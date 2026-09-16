---
description: "Task list for Payment Service"
---

# Tasks: Payment Service

**Input**: Design documents from `/specs/002-payment-service/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. SC-005 (50 simultaneous requests yield one payment) is enforced by a unique
constraint, so constitution Principle V requires it to run against a real PostgreSQL.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1–US4)

## Path Conventions

Service code under `server/src/Services/Payment/`, tests under
`server/tests/Ecommerce.Payment.Tests/`, per the plan's Source Code layout.

---

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 Create the four service projects — `Ecommerce.Payment.Domain`, `.Application`, `.Infrastructure`, `.WebApi` — under `server/src/Services/Payment/`, mirroring `server/src/Services/Inventory/`
- [ ] T002 Create the test project `server/tests/Ecommerce.Payment.Tests/Ecommerce.Payment.Tests.csproj` (xUnit)
- [ ] T003 Register all five projects in `server/Ecommerce.slnx` under `/src/Services/Payment/` and the existing `/tests/` folder
- [ ] T004 Add package and project references across the four `server/src/Services/Payment/Ecommerce.Payment.*/*.csproj` files, matching the versions used solution-wide (MassTransit 8.3.6, MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF Core 10.0.3) plus `Ecommerce.Shared` and `Ecommerce.Contracts`
- [ ] T005 [P] Add `postgres-payment` to `server/docker-compose.yml` on host port **5438**, database `ecommerce_payment_db`, matching the other database services
- [ ] T006 [P] Add `PAYMENT_DB_PORT=5438` and `PAYMENT_OUTCOME=Approve` to `server/.env.example` and the local `server/.env`
- [ ] T007 [P] Add the payments route, cluster and health route to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json` (`/api/payment/health` rewrites to `/health`)
- [ ] T008 [P] Add the migration and launch steps for Payment to `server/start-dev.ps1` and `server/start-dev.sh` (port 5061)

**Checkpoint**: `dotnet build` succeeds and `docker compose up -d` brings up the payment database.

---

## Phase 2: Foundational (Blocking Prerequisites)

- [ ] T009 [P] Create `PaymentStatus` enum (`Approved`, `Rejected`) in `server/src/Services/Payment/Ecommerce.Payment.Domain/Enums/PaymentStatus.cs`
- [ ] T010 [P] Create `Payment` in `server/src/Services/Payment/Ecommerce.Payment.Domain/Entities/Payment.cs` with `OrderId`, `UserId`, `Amount`, `Status`, `FailureReason`, `Provider`, `ProcessedAt`, per [data-model.md](./data-model.md)
- [ ] T011 Create `PaymentDbContext` in `.../Ecommerce.Payment.Infrastructure/Persistence/PaymentDbContext.cs`, applying configurations from the assembly and calling `AddTransactionalOutboxEntities()`
- [ ] T012 Create `PaymentConfiguration` in `.../Persistence/Configurations/PaymentConfiguration.cs`: table `payments`, **unique index on `OrderId`** (the guarantee for FR-005 and FR-006, not an optimisation), `Amount` as `numeric(18,2)`, `Status` and `Provider` as strings, `Provider` defaulting to `Stub`. Deliberately **no** check constraint on `Amount`: a rejection must still record the amount that was refused
- [ ] T013 Create the initial EF Core migration into `.../Ecommerce.Payment.Infrastructure/Migrations/` and apply it against the container on 5438
- [ ] T014 [P] Declare `IPaymentRepository` and `IUnitOfWork` in `.../Ecommerce.Payment.Application/Common/Interfaces/`, mirroring Inventory's split of staged writes from an explicit `SaveChangesAsync`
- [ ] T015 Implement `PaymentRepository` and `UnitOfWork` in `.../Infrastructure/Persistence/`, the latter running an operation inside one transaction as Inventory's does
- [ ] T016 [P] Create `PaymentOutcomeOptions` in `.../Infrastructure/Gateway/PaymentOutcomeOptions.cs`: the configured outcome (`Approve` default, `Reject`), **failing at startup on an unrecognised value** rather than silently picking one. This folder is the seam a real provider would replace
- [ ] T017 Create `DependencyInjection.cs` for Application and Infrastructure, matching Inventory's structure
- [ ] T018 Create `.../Ecommerce.Payment.WebApi/Program.cs`: the `.env` loader, connection string from `DB_USER`/`DB_PASSWORD`/`PAYMENT_DB_NAME`/`PAYMENT_DB_PORT`, **`JWT_SECRET` mapped into `JwtSettings:Secret`**, `PAYMENT_OUTCOME` mapped into configuration, `AddJwtAuthentication`, `AddExceptionHandler<GlobalExceptionHandler>`, and `app.Run("http://localhost:5061")` — pinned, not left to the default
- [ ] T019 Configure MassTransit in `Program.cs` with `AddEntityFrameworkOutbox<PaymentDbContext>` (`UsePostgres()`, `UseBusOutbox()`) and the consumer-side inbox via `AddConfigureEndpointsCallback`, plus the RabbitMQ host from `RABBITMQ_HOST`/`RABBITMQ_USER`/`RABBITMQ_PASS`
- [ ] T020 [P] Create `.../Ecommerce.Payment.WebApi/appsettings.json` with `Logging`, `AllowedHosts`, the `JwtSettings` section and a `Payment` section carrying the default outcome

**Checkpoint**: the service starts and `/health` returns 200.

---

## Phase 3: User Story 1 — A shopper's order completes (P1) 🎯 MVP

**Goal**: Payment approves, the saga reaches `OrderCompleted`, and Inventory deducts the held stock.

**Independent test**: Submit an order for an in-stock product; it completes and the units leave stock
permanently (quickstart scenario 1).

### Tests for User Story 1

- [ ] T021 [P] [US1] Write `PaymentTestFixture.cs` in `server/tests/Ecommerce.Payment.Tests/`, mirroring `InventoryTestFixture`: throwaway database per class against the real PostgreSQL on 5438, MassTransit test harness, and a **bounded connection pool** — feature 001's concurrency test failed on `max_connections` rather than on its assertion, and that lesson is carried, not relearned
- [ ] T022 [P] [US1] Write `ProcessPaymentTests.cs`: a valid request is approved, records one row with `Provider = "Stub"`, and publishes exactly one `PaymentProcessedEvent` and no `PaymentFailedEvent` (FR-001)

### Implementation for User Story 1

- [ ] T023 [US1] Implement `ProcessPaymentCommand` + handler in `.../Application/Payments/ProcessPayment/`: read any existing payment, decide the outcome from `PaymentOutcomeOptions`, insert, **publish the reply, then call `SaveChangesAsync` once** — the ordering constitution III makes non-negotiable
- [ ] T024 [US1] Handle the unique-violation race in the handler: catch it, re-read the winning row, and reply with **that** row's outcome. Dropping the message would leave the saga waiting forever; replying independently could contradict what was recorded ([data-model.md](./data-model.md) transaction rule 2)
- [ ] T025 [US1] Implement `ProcessPaymentConsumer` in `.../WebApi/Consumers/ProcessPaymentConsumer.cs`, dispatching through MediatR
- [ ] T026 [P] [US1] Log a warning at startup in `Program.cs` naming the service a stand-in and stating the configured outcome (FR-008, FR-012, research D3)

**Checkpoint**: quickstart scenario 1 passes — checkout completes without anyone publishing a
message by hand, for the first time in this repository.

---

## Phase 4: User Story 2 — An operator can see what was charged (P2)

**Goal**: A payment can be accounted for without database access.

**Independent test**: Complete an order, look up its payment, see amount, outcome, timestamp and
provider (quickstart scenario 2).

- [ ] T027 [P] [US2] Implement `GetPaymentByOrderIdQuery` + handler in `.../Application/Payments/Queries/GetPaymentByOrderId/`, throwing `NotFoundException` for an order never paid for
- [ ] T028 [P] [US2] Implement `GetPaymentsQuery` + handler (paged, filterable by `orderId` and `status`) in `.../Application/Payments/Queries/GetPayments/`
- [ ] T029 [US2] Implement `PaymentsController` in `.../WebApi/Controllers/PaymentsController.cs`, `[Authorize(Roles = "Admin")]` — a payment record names a user and an amount, so it is not shopper-facing. Add `ApiControllerBase` alongside it
- [ ] T030 [US2] Extend `/health` in `Program.cs` to report `provider` and `configuredOutcome` (FR-012) — the check that stops a deliberately rejecting service looking like an outage
- [ ] T031 [P] [US2] Write `PaymentQueryTests.cs`: a paid order returns its record; an unknown order is a clean not-found

**Checkpoint**: quickstart scenario 2 passes.

---

## Phase 5: User Story 3 — Never charged twice (P3)

**Goal**: Replay and concurrency both yield one payment.

**Independent test**: Request the same payment several times; one row, stock deducted once
(quickstart scenario 3).

- [ ] T032 [P] [US3] Write `PaymentIdempotencyTests.cs`: three deliveries of one request produce one row with an unchanged `ProcessedAt`, and each delivery still publishes a reply (the saga may have missed the first)
- [ ] T033 [P] [US3] Write `PaymentConcurrencyTests.cs` against the real PostgreSQL: 50 simultaneous requests for one order yield exactly one row and 50 replies (SC-005)
- [ ] T034 [US3] Verify the unique-violation path from T024 in `.../Application/Payments/ProcessPayment/ProcessPaymentCommandHandler.cs` under `server/tests/Ecommerce.Payment.Tests/PaymentConcurrencyTests.cs` and fix whatever it exposes — this is the task most likely to reveal a real defect

**Checkpoint**: quickstart scenarios 3 and 6 pass.

---

## Phase 6: User Story 4 — The rejection path can be exercised (P4)

**Goal**: The saga's compensation branch runs for real, for the first time.

**Independent test**: Set the service to reject, place an order, watch held stock return to
available (quickstart scenario 4).

- [ ] T035 [P] [US4] Write `PaymentRejectionTests.cs`: with the outcome set to `Reject`, a valid request is rejected with a reason naming the setting and records a `Rejected` row; with `Approve`, a non-positive amount is still rejected (FR-004) and the refused amount is recorded
- [ ] T036 [US4] Implement the rejection branch in the `ProcessPaymentCommand` handler: publish `PaymentFailedEvent` with the reasons from [messages.md](./contracts/messages.md), in the same single-transaction pattern as the approval path
- [ ] T037 [US4] Add a validator rejecting non-positive amounts in `.../Application/Payments/ProcessPayment/ProcessPaymentCommandValidator.cs`, and make sure the rejection is **recorded** rather than only replied to

**Checkpoint**: quickstart scenario 4 passes — `ReleaseInventoryCommand` flows and Inventory returns
the units, with no message published by hand.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T038 Add the `postgres-payment` service container to `.github/workflows/ci.yml` so the payment tests run on every push
- [ ] T039 [P] Update `docs/infrastructure/database-setup.md` with the payment database row (port 5438) and its migration commands
- [ ] T040 [P] Update `docs/README.md` service port table (Payment 5061) and `CLAUDE.md` service map
- [ ] T041 [P] Update `docs/architecture/saga-orchestration-roadmap.md`: the saga now runs end to end, and record that both branches — completion and compensation — are reachable
- [ ] T042 [P] Record in `CLAUDE.md` and the roadmap that **Payment is a stand-in that moves no money**, with the three signals from research D3. A future reader who misses this is the risk the whole design guards against
- [ ] T043 Run every scenario in `specs/002-payment-service/quickstart.md` end to end against real services and record the results in that file

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — **blocks every user story**
- **US1 (Phase 3)**: depends on Foundational
- **US2 (Phase 4)**: depends on Foundational; independent of US1 in code (queries, not the consumer)
- **US3 (Phase 5)**: depends on US1 — it hardens the handler US1 creates
- **US4 (Phase 6)**: depends on US1 for the same reason; both branch the same handler
- **Polish (Phase 7)**: depends on the stories being delivered

### Within Each User Story

- Tests before the implementation they describe, and they must fail first
- Domain before repositories, repositories before handlers, handlers before consumers
- The story's checkpoint is verified before the next priority starts

### Parallel Opportunities

- T005–T008 in Setup — four separate files
- T009, T010 in Foundational — two entity files
- **US2 can be built in parallel with US1/US3/US4** by a second person: queries and a controller,
  touching none of the consumer's files
- Test tasks within a story — separate test files

---

## Implementation Strategy

### MVP scope

Phases 1–3 (T001–T026). That delivers User Story 1: checkout completes end to end, with stock
correctly deducted. It is the smallest increment that closes the gap this feature exists for.

### Incremental delivery

1. Setup + Foundational → the service runs and answers `/health`
2. **+ US1 → MVP.** The saga completes for the first time. Stop and validate
3. + US2 → payments can be accounted for
4. + US3 → one payment per order under replay and concurrency
5. + US4 → the compensation branch finally runs
6. + Polish → CI covers it, and the docs say plainly that no money moves

### What this does not finish

No money is taken. The next real step is replacing `Infrastructure/Gateway/` with a provider
integration, at which point the guarantees built here start protecting actual money rather than a
row in a table.

---

## Notes

- 43 tasks: 8 setup, 12 foundational, 6 for US1, 5 for US2, 3 for US3, 3 for US4, 6 polish
- 7 test tasks, each asserting a stated success criterion rather than an implementation detail
- Commit after each task or logical group, with a scoped Conventional Commit message
