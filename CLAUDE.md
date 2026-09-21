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

Tests live in `server/tests/` — `Ecommerce.Inventory.Tests` (25 tests, PostgreSQL on 5437),
`Ecommerce.Payment.Tests` (13 tests, PostgreSQL on 5438), `Ecommerce.Order.Tests` (31 tests,
PostgreSQL on 5434), `Ecommerce.Catalog.Tests` (8 tests, PostgreSQL on 5433), `Ecommerce.Cart.Tests`
(10 tests, PostgreSQL on 5439) and `Ecommerce.Identity.Tests` (16 tests, PostgreSQL on 5435). They run against a **real PostgreSQL** — the guarantees under test are the
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

There is also an **end-to-end check of the checkout saga**,
[.github/scripts/verify-saga.sh](.github/scripts/verify-saga.sh) — the only check in this repository
that can see *between* services. It places a real order over HTTP with a real signed customer token,
follows it to a terminal state, and asserts the stock moved by exactly the amount ordered with
nothing left held. It needs **all six** services; without them it reports **skipped** rather than
passing quietly, unless `SAGA_E2E_REQUIRE_ALL=1` (which CI sets) makes a skip fatal.

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

It asserts on `QuantityOnHand` **and** `QuantityReserved`, never on the derived `QuantityAvailable`
alone — during the queue-collision bug `Available` was correct while the units stayed held, so a
check reading only that number would have passed. Payment resolves its outcome once at startup, so
one running Payment gives you one of the two branches; the script runs whichever one Payment reports
and exercising both means running it twice with Payment restarted between. `SAGA_E2E_SCENARIO`
forces a branch and exists for the negative control. Background in
[specs/007-saga-e2e-verification](specs/007-saga-e2e-verification/).

### Bruno collection

[bruno/](bruno/) is a [Bruno](https://www.usebruno.com/) collection (OpenCollection YAML) covering
**every public endpoint**, all through the gateway (`{{baseUrl}}` = `:5000`). Open the folder in
Bruno, pick the `local` environment, fill `adminEmail` / `adminPassword` (marked secret — Bruno keeps
the values out of the file; never commit them), then run the collection top to bottom: scripts carry
`customerToken`, `adminToken`, `categoryId`, `productId` and `orderId` from one request to the next,
and every request has tests. It runs headless too:

```bash
cd bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Adding or changing an endpoint means updating `bruno/` in the same change** — a new request file
with a status test, and the gateway route it goes through. `security-checks/` holds the negative
cases (401, 403, 400, 404); `duplicate registration is 409` is **known failing** (Identity throws a
bare `Exception`, so it returns 500) and stays red until that is fixed.

CI is [.github/workflows/ci.yml](.github/workflows/ci.yml): a `build` job, then **two smoke jobs side
by side** — `auth-smoke` (three services) and `saga-e2e` (six services plus RabbitMQ, both branches,
Payment restarted in between). They depend only on `build`, so they run concurrently, and `publish`
is gated on both. If adding unit tests there is no existing convention to follow — pick one and say
so.

## Service map

| Service | HTTP port | DB port / name | Notes |
| :-- | :-- | :-- | :-- |
| ApiGateway (YARP) | 5000 | — | routes configured in [appsettings.json](server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json) |
| Identity | 5056 (REST) + **6056 (gRPC)** | 5435 / `ecommerce_identity_db` | Signs tokens, seeds roles + first admin; **owns customers' delivery addresses** and serves `AddressReading` to Order; no MassTransit |
| Catalog | 5057 (REST) + **6057 (gRPC)** | 5433 / `ecommerce_catalog_db` | products/categories + outbox; consumes stock availability from Inventory; **serves `CatalogPricing` over h2c** |
| Orchestrator (Saga) | 5058 | 5436 / `ecommerce_saga_db` | MassTransit state machine, no controllers |
| Order | 5059 | 5434 / `ecommerce_order_db` | Checkout + outbox; settles to `Paid` on the saga's outcome; delivery options; Admin fulfilment (`Preparing` → `Shipped`); owner-scoped reads |
| Inventory | 5060 | 5437 / `ecommerce_inventory_db` | Stock + reservations; consumers + expiry sweeper |
| Payment | 5061 | 5438 / `ecommerce_payment_db` | **Stub gateway — approves without moving money** |
| Cart | 5062 (REST) + **6062 (gRPC)** | 5439 / `ecommerce_cart_db` | one cart per signed-in customer; **stores no price**; serves `CartReading` to Order at checkout |

pgAdmin `:5050`, RabbitMQ management `:15672`.

Ports are hardcoded in each `Program.cs` via `app.Run("http://localhost:50XX")` — except Identity, Catalog and Cart, which serve gRPC too and therefore declare **both** Kestrel endpoints (REST on `50XX`, gRPC on `51XX` on the host, `8080`/`8081` in a container) and have no `app.Run(url)`. Adding a service means adding a route **and** a cluster to the gateway's `ReverseProxy` config; health routes there rewrite `/api/<svc>/health` → `/health`.

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

**The orchestrator publishes through the transactional outbox, like everything else** (`AddEntityFrameworkOutbox<OrchestratorDbContext>` + `UseBusOutbox()`, with `AddTransactionalOutboxEntities()` in its `OnModelCreating`). It did not until `20260921104437_AddTransactionalOutbox`, and the consequence was that the first order after a cold start never settled — see the gotcha below. Principle III applies to the state machine exactly as it applies to a command handler.

⚠️ **Payment is a stand-in that moves no money.** It approves without contacting any provider. Three signals guard against mistaking it for the real thing, and all three must survive any refactor: `Provider = "Stub"` on every payment row, a warning logged at startup, and `/health` reporting both `provider` and `configuredOutcome`. `Infrastructure/Gateway/StubPaymentGateway.cs` is the seam a real integration replaces — everything around it already behaves as though money were real.

Inventory also consumes `OrderCompletedEvent` as its confirmation signal: there is no `ConfirmInventoryCommand` in the contracts, and the saga finalizes without telling Inventory anything. Without that consumer a successful order would keep its units held until the sweeper returned them to the shelf.

**Order consumes `OrderCompletedEvent` and `OrderFailedEvent` too**, and settles its own row with a guarded `UPDATE ... WHERE Status = 'Submitted'` so a redelivery affects zero rows. Note `OrderFailedEvent` is published on **both** failure branches — reservation failure and payment rejection.

**Since feature 011 a successful checkout settles to `Paid`, not `Completed`** — the event keeps its
name, only Order's own status changed; `Completed` is still in the enum so old rows and a rolled-back
image parse, and every read reports it as `Paid`. After `Paid`, **only an Admin** moves an order to
`Preparing` and then `Shipped` (with a tracking reference), through guarded single-statement updates;
the saga still ends at payment. `Pending`, `StockReserved` and `Cancelled` remain unreachable. See
[specs/011-order-shipping](specs/011-order-shipping/) and the history in
[specs/003-order-lifecycle/data-model.md](specs/003-order-lifecycle/data-model.md).

The roadmap is in [docs/architecture/saga-orchestration-roadmap.md](docs/architecture/saga-orchestration-roadmap.md) (Phases 1–6.5 done, Phase 7 = observability/Seq/E2E is next).

### Authentication
`Ecommerce.Shared/Authentication/` holds the whole story. Identity **signs** tokens; every other
service **validates** them by calling `AddJwtAuthentication(builder.Configuration)`, which also
registers `ICurrentUser` — how the Application layer learns who the caller is without touching
`HttpContext`. Three settings in there are load-bearing and easy to break:
`MapInboundClaims = false` (otherwise `sub` is renamed), `RoleClaimType = "role"` (the signing side
writes the short name, not the `ClaimTypes.Role` URI), and `ClockSkew = Zero`.

**Neither does the price.** `OrderItemRequest` carries only `ProductId` and `Quantity`; Order asks
Catalog over gRPC at submission and **freezes** the price and name onto the order line. Before
feature 009 the price came from the request body and a product listed at 40,000,000 was bought for
1 — same shape of defect as `UserId`, on the thing a shop exists to get right. The fields are
**removed, not ignored**, for the same reason `UserId` was.

**Checkout now reads the cart too** (feature 010). `POST /api/orders` takes **no body**: Order asks
Cart for the caller's cart over gRPC, **forwarding the customer's bearer token** so Cart identifies
them through `ICurrentUser` like every service does. The request message is empty on purpose — a
`GetCart(userId)` would let anything on the network read anybody's cart, the `UserId`-in-the-body
defect one hop further in. So checkout depends synchronously on **Catalog and Cart** — and, since
feature 011, **Identity**: the body names an `addressId` and a `shippingOption`, Order reads the
address from Identity over gRPC with the same forwarded token, and **freezes a copy** of it and of
the option's name and price onto the order. `TotalAmount` = items + delivery, so the saga charges
delivery with no contract change. Someone else's address id is a 404, indistinguishable from a
missing one.

**The cart removes what was ordered on `OrderCompletedEvent`, not on submission** — `Failed` is
terminal, and removing at submission would leave a customer whose card was declined with an empty
cart and a dead order. `OrderCompletedEvent` carries only an order id, so Cart also consumes
`OrderSubmittedEvent` to learn the items, and because nothing orders delivery across message types
(which is what #15 was) **whichever of the two arrives second applies the removal**, guarded by an
`Applied` flag under `FOR UPDATE`. Removal is a *decrement*, so anything added during checkout
survives. See [specs/010-customer-cart](specs/010-customer-cart/).

**Order → Catalog was the first synchronous cross-service call in the system**, and it costs something real:
Catalog being unreachable now refuses orders (**503**, via `DependencyUnavailableException`) where
before they were placed at whatever price the customer claimed. Catalog serves gRPC on a **second
port** because one plaintext port cannot carry HTTP/1.1 and HTTP/2 — telling them apart needs ALPN,
which is part of TLS. Verified on .NET 10; `Http1AndHttp2` on a plaintext endpoint does **not**
serve h2c. Background: [docs/architecture/service-to-service-communication.md](docs/architecture/service-to-service-communication.md)
and [specs/009-catalog-owns-price](specs/009-catalog-owns-price/).

**The user id never comes from the request body.** `SubmitOrderCommand` deliberately has no
`UserId`; the handler reads it from `ICurrentUser`. Keep it that way for new commands.

Roles (`Admin`, `Customer`) and the first administrator are seeded at Identity startup by
`DataInitializer`, from `ADMIN_EMAIL` / `ADMIN_PASSWORD`. The bootstrap path closes as soon as any
admin exists. Self-registration always grants `Customer`.

### Shared building blocks
**Inventory owns stock; Catalog reports a read model of it.** `Product.Availability` is fed by `StockAvailabilityChangedEvent` and surfaces as `"InStock"` / `"OutOfStock"` — never a count. **Nothing may sell against it**: checkout reserves under `FOR UPDATE` against Inventory's row, and a read model fed by messages is seconds behind by design. A real number comes from `GET /api/stock/{productId}` on Inventory, which is public. Six handlers move stock and every one must announce — if you add a seventh, it must call `StockAvailabilityAnnouncer` too, and `Ecommerce.Inventory.Tests/AnnouncementTests.cs` is what catches the omission. Background in [specs/004-stock-single-source](specs/004-stock-single-source/).

- **`Ecommerce.Contracts`** — the only cross-service coupling allowed: message records grouped by owning domain (`Catalog/`, `Order/`, `Inventory/`, `Payment/`). Pure records, no dependencies. Any new integration event goes here — but only once something publishes it and something consumes it. A record with neither states that a service says something it does not say; `Identity/UserRegisteredEvent` sat here unused until `81b7551`.
- **`Ecommerce.Shared`** — `GlobalExceptionHandler` (RFC 7807 ProblemDetails; maps `ValidationException` → 400 with an `errors` extension, `NotFoundException` → 404, `ConflictException` → 409, detail hidden outside Development) and `ValidationBehavior` (MediatR open behavior that throws on validator failures). Wired with `AddExceptionHandler<GlobalExceptionHandler>()` + `AddProblemDetails()` + `app.UseExceptionHandler()`.

Catalog used to carry dead duplicates of both; they were deleted in `763b77a`. There is now exactly one `ValidationBehavior`, one `GlobalExceptionHandler` and one each of `NotFoundException` / `ConflictException`, all in `Ecommerce.Shared`. **Don't reintroduce a per-service copy** — two exception types with the same name in two namespaces compiles, reviews clean, and falls through the shared handler to a 500, because that handler pattern-matches on `Ecommerce.Shared.Exceptions`. Throw `Ecommerce.Shared.Exceptions.*` from handlers.

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
- **A saga's `.Publish(...)` needs the outbox as much as a handler's does.** Without
  `UseBusOutbox()`, a state machine activity's publish reaches the broker *during* the consume,
  before the instance that caused it is committed — and the reply can arrive first, find no
  instance to correlate to, and be **discarded with no fault, no error queue and no log line**.
  Measured on a cold start with MassTransit at `Debug`: `InventoryReservedEvent` finished in 0.29s
  while `OrderSubmittedEvent` was still 5.4s from committing, because the first message a process
  handles pays for JIT, the EF model build and the first connection. The order stayed `Submitted`
  forever and its stock stayed held; four sagas were stranded that way between 2026-09-03 and
  09-17 and nobody noticed, because the second order of any session works. Fixed in
  [#15](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/15); the symptom is
  what `verify-saga.sh` reports as a **stall**.
- **The Orchestrator has no `/health` endpoint** — it has no controllers, so `GET :5058/health` is a
  404, and `docker-compose.app.yml` disables its health check for that reason. It is therefore the
  one service nothing can wait for or probe, which is why an order that never leaves `Submitted`
  usually means the Orchestrator rather than anything the check could test. The constitution says
  every service exposes `/health`; this one does not, and that disagreement is recorded but not yet
  resolved.
- **Validation silently skipped every command that returns nothing — until feature 010.** The
  shared `ValidationBehavior` was constrained to `where TRequest : IRequest<TResponse>`, and in
  MediatR 12 a void command implements `IRequest`, a *separate* interface. The pipeline asked for
  `IPipelineBehavior<TCommand, Unit>`, the constraint could not be met, and the behavior was
  dropped without a word — the cart accepted a quantity of `-1` with 204 despite a `GreaterThan(0)`
  rule. Now `where TRequest : notnull`. `Ecommerce.Cart.Tests/ValidationTests` is what fails if it
  regresses.
- **A new service needs its own `appsettings.json` with `JwtSettings`.** Without it `Issuer` and
  `Audience` are empty and **every** token is rejected with 401 (`IDX10208: Unable to validate
  audience`) — while the service starts and reports healthy. The constitution says a missing
  required setting must fail at startup; `AddJwtAuthentication` does not, which is a gap in the
  shared building block, recorded rather than fixed.
- **An application-generated `Guid` key needs `ValueGeneratedNever()`.** By convention a `Guid`
  key is `ValueGeneratedOnAdd`, so a new child discovered through a navigation collection with its
  id already set is taken for an *existing* row: EF issues an `UPDATE`, it affects zero rows, and
  the save throws `DbUpdateConcurrencyException`. All eight Cart tests failed that way first time.
- **Calling `ListenAnyIP` at all replaces `ASPNETCORE_URLS` — it does not add to it.** Configuring
  only the gRPC endpoint on Catalog silently unbound REST: the container came up listening on 8081
  alone, answered nothing on 8080, and went unhealthy. Kestrel says so in a warning nobody reads —
  `Overriding address(es) 'http://+:8080'`. Both endpoints are now declared together, and the
  `app.Run(url)` fallback was removed because it would be a third opinion about where to listen.
- **Adding a project means adding a line to [server/Dockerfile](server/Dockerfile).** It copies each
  `.csproj` by name before `dotnet restore`, then publishes with `--no-restore` — so a missing line
  fails at publish with a message about the project rather than about the list.
  `Ecommerce.Contracts.Grpc` cost exactly that on its first container build.
- **A failed `SaveChangesAsync` does not untrack what it tried to write.** The rows stay `Added`, so
  the next save on that same context re-attempts them. Catching a unique violation and then saving
  again therefore raises the *same* violation, outside the catch — which is how Payment's
  `Fifty_simultaneous_requests_record_exactly_one_payment` broke CI on a tree byte-identical to one
  that had passed on a pull request minutes earlier. Payment's `IUnitOfWork.DiscardPendingChanges()`
  (`ChangeTracker.Clear()`) is called before the recovery read for exactly this reason, and it
  discards the staged outbox message with it — correctly, since that message described a payment the
  database never accepted.
- **The `.env` loader falls back; it no longer overrides.** Precedence is
  `environment variable > .env > appsettings.json > hardcoded default`. Before feature 005 it was
  the other way round and `PAYMENT_OUTCOME=Reject dotnet run ...` was silently ignored; it now
  works. If you relied on the file winning, that is the change.
- Services installed natively on the host silently shadow the compose containers when they share a
  port. Identity's database is published on `5435` rather than `5432` for exactly this reason. The
  services connect to RabbitMQ over the default `5672` and the code passes no port, so a native
  broker on that port wins — stop it and let the container have it. Documented in
  [docs/guides/troubleshooting.md](docs/guides/troubleshooting.md) §6.

## Running in containers

`docker compose up -d` still brings up **infrastructure only**, which is what `start-dev.sh`
expects. The seven services live in an overlay:

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # everything
docker compose up -d                                                           # infra only
```

Host ports are unchanged (5000, 5056-5061); inside their containers every service binds 8080. One
[Dockerfile](server/Dockerfile) builds all seven, selected by a `PROJECT` build argument.

Three traps, each of which cost time to find:

- **`*_DB_PORT` is `5432` inside the container network.** The 5433-5438 in `.env` are *host*
  publications. Getting this wrong looks like a dead database.
- **The gateway's YARP destinations are overridden by command-line arguments, not environment
  variables.** The documented `ReverseProxy__Clusters__<name>__Destinations__destination1__Address`
  form does **not** bind — verified, with `Logging__LogLevel__Default` taking effect on the same
  container, so the environment provider itself works. Cause not established; see
  [research D4](specs/005-containerise-services/research.md).
- **A leftover host process shadows a container's published port.** A stray `dotnet run` from an
  earlier session answered on 5061 while the Payment container sat behind it, and the symptom was an
  order failing for no visible reason. Before trusting any container result:
  `Get-Process | Where-Object { $_.ProcessName -like 'Ecommerce.*' }` must be empty.

`server/.dockerignore` is what keeps `.env` out of an image — **Docker does not read `.gitignore`**.
[.github/scripts/verify-image-has-no-secrets.sh](.github/scripts/verify-image-has-no-secrets.sh)
checks every layer, not the running container, and CI runs it.

## Published images

A merge to `main` whose checks pass publishes seven images to GHCR, each built, scanned for
credentials, and only then pushed:

```text
ghcr.io/jessiicamaru/ecommerce-<service>:sha-<short-sha>   # immutable; the only deployable name
ghcr.io/jessiicamaru/ecommerce-<service>:main              # moves; convenience only
```

**`:main` cannot answer "the previous version"**, which is why nothing deployable may be named by it.
A pull request publishes nothing. See
[specs/006-release-and-rollback/contracts/release-artifacts.md](specs/006-release-and-rollback/contracts/release-artifacts.md).

**What makes `sha-` immutable is a check, not a convention.** Before pushing a `sha-` tag the
release asks the registry whether it already resolves and skips it if so, so re-running a publish
never rewrites an existing one — and re-running is therefore the *correct* way to finish a release
that stopped partway, rather than the way to corrupt it. It was not always so: on 2026-09-21 a
re-run silently changed what `sha-2cee71a` meant for three services. The job ends by asking the
registry whether all seven names exist, so a green publish means the release is whole rather than
that the steps ran. Overwriting on purpose is possible only through a manual `workflow_dispatch`
with `force_republish`, which no merge can reach. Details and the one remaining hole —
`denied` cannot distinguish "no such package" from "no permission to read it" — are in
[specs/008-immutable-release-tags/contracts/publish-behaviour.md](specs/008-immutable-release-tags/contracts/publish-behaviour.md).

**A schema change must not strand an earlier image.** Dropping, renaming or narrowing a column means
redeploying a previous version takes the service down rather than restoring it — split it into expand
then contract. The constitution states the rule; a `schema-compatibility` job comments on any pull
request that adds a migration doing one of those, and never blocks.

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
