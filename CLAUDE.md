# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

.NET 10 e-commerce backend built as microservices. Each service is Clean Architecture (Domain → Application → Infrastructure → WebApi), owns its own PostgreSQL database, and communicates asynchronously over RabbitMQ via MassTransit. A standalone Saga Orchestrator coordinates the checkout flow; YARP fronts everything as an API Gateway.

There is no frontend and no test project in the repo yet — everything lives under [server/](server/).

## Commands

All commands run from `server/` (the solution root; solution file is `Ecommerce.slnx`).

```bash
# One-click: docker compose up + migrations + launch all 5 services in separate windows
./start-dev.ps1        # Windows PowerShell
./start-dev.sh         # Git Bash / Linux

# Infrastructure only (4 Postgres containers, RabbitMQ, pgAdmin)
docker compose up -d

dotnet build                                          # whole solution
dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/   # single service
```

EF Core migrations — note the Orchestrator is the one service where `--project` and `--startup-project` are the same (its DbContext lives in the WebApi project):

```bash
dotnet ef migrations add <Name> --project src/Services/Identity/Ecommerce.Identity.Infrastructure/     --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
dotnet ef database update      --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/       --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/
dotnet ef database update      --project src/Services/Order/Ecommerce.Order.Infrastructure/           --startup-project src/Services/Order/Ecommerce.Order.WebApi/
dotnet ef database update      --project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/     --startup-project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/
```

Tests live in `server/tests/` — `Ecommerce.Inventory.Tests` (19 tests, PostgreSQL on 5437),
`Ecommerce.Payment.Tests` (11 tests, PostgreSQL on 5438) and `Ecommerce.Order.Tests` (15 tests,
PostgreSQL on 5434). They run against a **real PostgreSQL** — the guarantees under test are the
database's row locking, unique constraints and guarded updates, so an in-memory provider would pass
against code that oversells or re-settles a finished order. Run them with `DB_PASSWORD` set:

```bash
cd server
DB_PASSWORD=<your password> dotnet test
```

There is also an end-to-end auth check,
[.github/scripts/verify-auth.sh](.github/scripts/verify-auth.sh), which CI runs and which also runs
locally against started services. It needs Identity and Catalog; if Order is also up it additionally
checks order ownership with real signed tokens — the one place that path is exercised without a
substituted `ICurrentUser`. When Order is not running it says the checks were **skipped** rather than
passing quietly:

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-auth.sh
```

CI is [.github/workflows/ci.yml](.github/workflows/ci.yml): a `build` job, then an `auth-smoke` job
that spins up PostgreSQL service containers, migrates, starts Identity and Catalog, and runs that
script. If adding unit tests there is no existing convention to follow — pick one and say so.

## Service map

| Service | HTTP port | DB port / name | Notes |
| :-- | :-- | :-- | :-- |
| ApiGateway (YARP) | 5000 | — | routes configured in [appsettings.json](server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json) |
| Identity | 5056 | 5435 / `ecommerce_identity_db` | Signs tokens, seeds roles + first admin; no MassTransit yet |
| Catalog | 5057 | 5433 / `ecommerce_catalog_db` | products/categories + outbox |
| Orchestrator (Saga) | 5058 | 5436 / `ecommerce_saga_db` | MassTransit state machine, no controllers |
| Order | 5059 | 5434 / `ecommerce_order_db` | Submit + outbox; settles on the saga's outcome, owner-scoped reads |
| Inventory | 5060 | 5437 / `ecommerce_inventory_db` | Stock + reservations; consumers + expiry sweeper |
| Payment | 5061 | 5438 / `ecommerce_payment_db` | **Stub gateway — approves without moving money** |

pgAdmin `:5050`, RabbitMQ management `:15672`.

Ports are hardcoded in each `Program.cs` via `app.Run("http://localhost:50XX")` (Identity is the exception — it uses launchSettings). Adding a service means adding a route **and** a cluster to the gateway's `ReverseProxy` config; health routes there rewrite `/api/<svc>/health` → `/health`.

## Architecture

### Layer dependencies (per service)
`Domain` (no dependencies) ← `Application` (MediatR, FluentValidation, MassTransit.Abstractions) ← `Infrastructure` (EF Core, Npgsql) ← `WebApi` (MassTransit.RabbitMQ, controllers).

Application defines repository interfaces in `Common/Interfaces/`; Infrastructure implements them and registers both in the layer's own `DependencyInjection.cs` (`AddApplication()` / `AddInfrastructure(configuration)`), which `Program.cs` calls.

### CQRS convention
Features are foldered by use case: `Application/<Aggregate>/Commands/<UseCase>/` containing `XCommand.cs` (a `record` implementing `IRequest<TResponse>`), `XCommandHandler.cs`, `XCommandValidator.cs`; queries mirror this under `Queries/`. Response DTOs are positional `record`s in `<Aggregate>/Common/`.

Controllers derive from the per-service `ApiControllerBase` (`[ApiController]`, `[Route("api/[controller]")]`, lazy `Mediator` property) and do nothing but `Mediator.Send(...)` — commands are bound straight from `[FromBody]`.

### Transactional Outbox (the critical pattern)
Services that publish events register `AddEntityFrameworkOutbox<TDbContext>` with `UsePostgres()` + `UseBusOutbox()`, and their DbContext calls `modelBuilder.AddTransactionalOutboxEntities()` in `OnModelCreating`.

**Order matters in handlers**: stage the entity, then `_publishEndpoint.Publish(...)`, *then* `SaveChangesAsync()` — one atomic transaction writing both the row and the OutboxMessage. Publishing after `SaveChangesAsync` breaks atomicity (this was the subject of two prior fix commits). See [SubmitOrderCommandHandler.cs](server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommandHandler.cs) as the reference. Note repositories expose `AddAsync` and `SaveChangesAsync` separately precisely to make this sequencing possible.

### Saga
[OrderStateMachine.cs](server/src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/StateMachines/OrderStateMachine.cs) drives `OrderSubmitted → ReserveInventory → ProcessPayment → OrderCompleted`, with `ReleaseInventoryCommand` compensation on payment failure. All events correlate on `OrderId`; the instance is `OrderStateData` persisted through `SagaDbContext` with `ConcurrencyMode.Optimistic`.

**The saga now runs end to end.** Submit → reserve → pay → complete, with stock permanently deducted, and no message published by hand. Both branches are reachable: setting `PAYMENT_OUTCOME=Reject` exercises the compensation path, which releases the held stock.

⚠️ **Payment is a stand-in that moves no money.** It approves without contacting any provider. Three signals guard against mistaking it for the real thing, and all three must survive any refactor: `Provider = "Stub"` on every payment row, a warning logged at startup, and `/health` reporting both `provider` and `configuredOutcome`. `Infrastructure/Gateway/StubPaymentGateway.cs` is the seam a real integration replaces — everything around it already behaves as though money were real.

Inventory also consumes `OrderCompletedEvent` as its confirmation signal: there is no `ConfirmInventoryCommand` in the contracts, and the saga finalizes without telling Inventory anything. Without that consumer a successful order would keep its units held until the sweeper returned them to the shelf.

**Order consumes `OrderCompletedEvent` and `OrderFailedEvent` too**, and settles its own row with a guarded `UPDATE ... WHERE Status = 'Submitted'` so a redelivery affects zero rows. Note `OrderFailedEvent` is published on **both** failure branches — reservation failure and payment rejection. Four `OrderStatus` values are unreachable by design (`Pending`, `StockReserved`, `Paid`, `Cancelled`); see [specs/003-order-lifecycle/data-model.md](specs/003-order-lifecycle/data-model.md) before assuming one of them can happen.

The roadmap is in [docs/architecture/saga-orchestration-roadmap.md](docs/architecture/saga-orchestration-roadmap.md) (Phases 1–6.5 done, Phase 7 = observability/Seq/E2E is next).

### Authentication
`Ecommerce.Shared/Authentication/` holds the whole story. Identity **signs** tokens; every other
service **validates** them by calling `AddJwtAuthentication(builder.Configuration)`, which also
registers `ICurrentUser` — how the Application layer learns who the caller is without touching
`HttpContext`. Three settings in there are load-bearing and easy to break:
`MapInboundClaims = false` (otherwise `sub` is renamed), `RoleClaimType = "role"` (the signing side
writes the short name, not the `ClaimTypes.Role` URI), and `ClockSkew = Zero`.

**The user id never comes from the request body.** `SubmitOrderCommand` deliberately has no
`UserId`; the handler reads it from `ICurrentUser`. Keep it that way for new commands.

Roles (`Admin`, `Customer`) and the first administrator are seeded at Identity startup by
`DataInitializer`, from `ADMIN_EMAIL` / `ADMIN_PASSWORD`. The bootstrap path closes as soon as any
admin exists. Self-registration always grants `Customer`.

### Shared building blocks
- **`Ecommerce.Contracts`** — the only cross-service coupling allowed: message records grouped by owning domain (`Catalog/`, `Order/`, `Inventory/`, `Payment/`). Pure records, no dependencies. Any new integration event goes here.
- **`Ecommerce.Shared`** — `GlobalExceptionHandler` (RFC 7807 ProblemDetails; maps `ValidationException` → 400 with an `errors` extension, `NotFoundException` → 404, `ConflictException` → 409, detail hidden outside Development) and `ValidationBehavior` (MediatR open behavior that throws on validator failures). Wired with `AddExceptionHandler<GlobalExceptionHandler>()` + `AddProblemDetails()` + `app.UseExceptionHandler()`.

Catalog still carries **dead duplicates** of both — `Catalog.Application/Common/Behaviors/ValidationBehavior.cs`, `Catalog.WebApi/Middlewares/GlobalExceptionHandler.cs`, `Catalog.Domain/Exceptions/*`. The shared versions are the live ones (`Program.cs` and `DependencyInjection.cs` reference `Ecommerce.Shared`). Don't extend the copies; throw `Ecommerce.Shared.Exceptions.*` from handlers.

## Conventions

- **Primary keys are `Guid`** — ADR-001 mandates `Guid.CreateVersion7()` (time-ordered) over `Guid.NewGuid()`. Existing code has not caught up; prefer v7 in new code.
- Primary-constructor DI with a `private readonly` field assignment is the house style for handlers, repositories, and services.
- EF mapping is Fluent API only, in `IEntityTypeConfiguration<T>` classes picked up by `ApplyConfigurationsFromAssembly`. Tables are **snake_case plural** (`"products"`, `"orders"`); money is `decimal(18,2)`; enums persist as strings via `HasConversion<string>()`.
- Every service's `Program.cs` opens with an identical hand-rolled `.env` loader that walks parent directories, then *overrides* `ConnectionStrings:DefaultConnection` by composing it from env vars (`DB_USER`, `DB_PASSWORD`, `<SVC>_DB_NAME`, `<SVC>_DB_PORT`). The values in `appsettings.json` are effectively ignored — change the env vars, not the JSON.

### Gotchas
- `server/.env` is gitignored; copy from `.env.example`. `docker compose` needs `DB_USER`/`DB_PASSWORD`/`PGADMIN_*`/`RABBITMQ_USER`/`RABBITMQ_PASSWORD` or the containers come up with empty credentials.
- The services read **`RABBITMQ_PASS`**, but `.env.example` and compose use **`RABBITMQ_PASSWORD`** — a non-default RabbitMQ password requires both names set.
- `DB_PORT` variables (`CATALOG_DB_PORT` etc.) aren't in `.env.example`; the per-service defaults in `Program.cs` are the real source of truth.
- Hosts are hardcoded to `localhost` in connection strings, so the services are not container-ready as written.
- **A consumer's class name becomes its queue name.** Two services with a consumer class of the same
  name bind to the *same* queue and compete for it, so a published event reaches one of them instead
  of both. This happened: Inventory and Order both had `OrderCompletedConsumer`, the order settled
  and the stock stayed held. Order now calls
  `SetEndpointNameFormatter(new DefaultEndpointNameFormatter(prefix: "OrderSvc", ...))`. Check
  `docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages consumers` — a queue with
  **2 consumers** that should have one subscriber per service is the symptom.
- **The `.env` loader overrides real environment variables**, it does not fall back to them. Every
  `Program.cs` calls `Environment.SetEnvironmentVariable` for each line in `.env`, so
  `PAYMENT_OUTCOME=Reject dotnet run ...` is silently ignored when `.env` sets it. Edit `.env` (and
  put it back), or delete the line.
- Services installed natively on the host silently shadow the compose containers when they share a
  port. Identity's database is published on `5435` rather than `5432` for exactly this reason. The
  services connect to RabbitMQ over the default `5672` and the code passes no port, so a native
  broker on that port wins — stop it and let the container have it. Documented in
  [docs/guides/troubleshooting.md](docs/guides/troubleshooting.md) §6.

## Project constitution

[.specify/memory/constitution.md](.specify/memory/constitution.md) holds the ratified principles
this project is held to — service autonomy, Clean Architecture layering, atomic writes with
idempotent messaging (non-negotiable), identity from the token, and evidence over assumption. It
supersedes convention and habit: where this file and the constitution disagree, the constitution
wins and this file is what gets corrected.

Feature designs are checked against it in `/speckit-plan`. A violation belongs in that plan's
Complexity Tracking with a justification and the rejected simpler alternative — not waived.

## Documentation

[docs/](docs/) is substantial and kept current — [docs/README.md](docs/README.md) is the index. Consult the architecture docs before design changes, and update the relevant one alongside the code (recent commits do this consistently).

`.agents/` is a vendored third-party agent kit (AG Kit) targeting Gemini CLI / Antigravity, not instructions for this codebase. The one convention from it worth honoring: branch names as `feature/<task-slug>` or `fix/<bug-slug>`.

Commit messages follow Conventional Commits with a scope, e.g. `feat(order): ...`, `fix(outbox): ...`, `docs: ...`.
