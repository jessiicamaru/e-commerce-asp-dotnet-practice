---
description: "Task list for Inventory Reservations"
---

# Tasks: Inventory Reservations

**Input**: Design documents from `/specs/001-inventory-reservations/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. Not optional here — SC-004 (no overselling across 100 concurrent orders) and
SC-005 (replay changes nothing) are concurrency properties that cannot be checked by hand, research
D7 settles the approach, and constitution Principle V requires the real dependency to be exercised
for the property under test.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1–US4)

## Path Conventions

Service code lives under `server/src/Services/Inventory/`, tests under
`server/tests/Ecommerce.Inventory.Tests/`, per the plan's Source Code layout.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Bring the new service into the solution and the local environment.

- [X] T001 Create the four service projects — `Ecommerce.Inventory.Domain`, `.Application`, `.Infrastructure`, `.WebApi` — under `server/src/Services/Inventory/`, mirroring the folder layout of `server/src/Services/Catalog/`
- [X] T002 Create the test project `server/tests/Ecommerce.Inventory.Tests/Ecommerce.Inventory.Tests.csproj` (xUnit), the first test project in the repository
- [X] T003 Register all five projects in `server/Ecommerce.slnx` under a new `/src/Services/Inventory/` folder and a `/tests/` folder
- [X] T004 Add package and project references across the four `server/src/Services/Inventory/Ecommerce.Inventory.*/*.csproj` files, matching the versions already used solution-wide: MassTransit 8.3.6 (`.Abstractions` in Application, `.EntityFrameworkCore` + `.RabbitMQ` in WebApi), MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF Core 10.0.3, plus references to `Ecommerce.Shared` and `Ecommerce.Contracts`
- [X] T005 [P] Add the `postgres-inventory` service to `server/docker-compose.yml` on host port **5437** (container 5432), database `ecommerce_inventory_db`, with the same volume and restart policy as the existing database services
- [X] T006 [P] Add `INVENTORY_DB_PORT=5437` and `INVENTORY_RESERVATION_TTL_MINUTES=15` to `server/.env.example`, and the same to the local `server/.env`
- [X] T007 [P] Add the inventory route, cluster and health route to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`, following the existing entries exactly (`/api/inventory/health` rewrites to `/health`)
- [X] T008 [P] Add the migration and launch steps for Inventory to `server/start-dev.ps1` and `server/start-dev.sh` (port 5060)

**Checkpoint**: `dotnet build` succeeds and `docker compose up -d` brings up the inventory database.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Everything every story needs. No user story can be built until this phase is complete.

⚠️ Product registration and stock intake live here rather than in a story: a reservation cannot be
tested at all until a product exists in inventory with units on it.

### Domain and persistence

- [X] T009 [P] Create `StockItem` in `server/src/Services/Inventory/Ecommerce.Inventory.Domain/Entities/StockItem.cs` with `ProductId`, `Sku`, `QuantityOnHand`, `QuantityReserved`, timestamps, per [data-model.md](./data-model.md)
- [X] T010 [P] Create `StockReservation` in `server/src/Services/Inventory/Ecommerce.Inventory.Domain/Entities/StockReservation.cs` with `OrderId`, `ProductId`, `Quantity`, `Status`, `ExpiresAt`, `SettledAt`, `SettlementReason`
- [X] T011 [P] Create `ReservationStatus` enum (`Held`, `Released`, `Confirmed`, `Expired`) in `server/src/Services/Inventory/Ecommerce.Inventory.Domain/Enums/ReservationStatus.cs`
- [X] T012 Create `InventoryDbContext` in `server/src/Services/Inventory/Ecommerce.Inventory.Infrastructure/Persistence/InventoryDbContext.cs`, applying configurations from the assembly and calling `AddTransactionalOutboxEntities()` — this is what gives the inbox its dedup tables
- [X] T013 [P] Create `StockItemConfiguration` in `.../Persistence/Configurations/StockItemConfiguration.cs`: table `stock_items`, unique index on `ProductId`, **check constraints** `QuantityOnHand >= 0` and `QuantityReserved BETWEEN 0 AND QuantityOnHand`. The constraints are required by constitution Principle III, not optional belt-and-braces
- [X] T014 [P] Create `StockReservationConfiguration` in `.../Persistence/Configurations/StockReservationConfiguration.cs`: table `stock_reservations`, **unique index on `(OrderId, ProductId)`**, check `Quantity > 0`, `Status` stored as string, index on `(Status, ExpiresAt)` for the sweeper
- [X] T015 Create the initial EF Core migration into `server/src/Services/Inventory/Ecommerce.Inventory.Infrastructure/Migrations/` (project = Infrastructure, startup-project = WebApi) and apply it against the container on 5437

### Repositories

- [X] T016 [P] Declare `IStockRepository` and `IReservationRepository` in `server/src/Services/Inventory/Ecommerce.Inventory.Application/Common/Interfaces/`, exposing staged writes and an explicit `SaveChangesAsync` so handlers can control the transaction boundary, as the other services do
- [X] T017 Implement `StockRepository` in `.../Infrastructure/Persistence/Repositories/StockRepository.cs`, including a `GetForUpdateAsync(IEnumerable<Guid> productIds, ...)` that issues `SELECT ... FOR UPDATE` **ordered by `ProductId` ascending**. The ordering is the deadlock guard from [data-model.md](./data-model.md) — two orders sharing two products in opposite order would otherwise deadlock
- [X] T018 [P] Implement `ReservationRepository` in `.../Infrastructure/Persistence/Repositories/ReservationRepository.cs`, with guarded status transitions written as conditional updates so a repeat affects zero rows

### Service wiring

- [X] T019 Create `DependencyInjection.cs` for Application (MediatR + validators + `ValidationBehavior` from `Ecommerce.Shared`) and for Infrastructure (DbContext + repositories), matching Catalog's structure
- [X] T020 Create `server/src/Services/Inventory/Ecommerce.Inventory.WebApi/Program.cs`: the `.env` loader used by every service, connection string composed from `DB_USER`/`DB_PASSWORD`/`INVENTORY_DB_NAME`/`INVENTORY_DB_PORT`, **`JWT_SECRET` mapped into `JwtSettings:Secret`** (omitting this is the documented way to have every token rejected), `AddJwtAuthentication`, `AddExceptionHandler<GlobalExceptionHandler>`, health checks, and `app.Run("http://localhost:5060")` — pinned, not left to the default
- [X] T021 Configure MassTransit in `Program.cs` with `AddEntityFrameworkOutbox<InventoryDbContext>` (`UsePostgres()`, `UseBusOutbox()`) **and consumer-side inbox**, plus RabbitMQ host from `RABBITMQ_HOST`/`RABBITMQ_USER`/`RABBITMQ_PASS`
- [X] T022 [P] Create `server/src/Services/Inventory/Ecommerce.Inventory.WebApi/appsettings.json` with `Logging`, `AllowedHosts` and the `JwtSettings` section (`Issuer: EcommerceApi`, `Audience: EcommerceClients`, empty `Secret`)

### Product registration and stock intake (FR-014, FR-015)

- [X] T023 Implement `ProductCreatedConsumer` in `.../WebApi/Consumers/ProductCreatedConsumer.cs`: register a `stock_items` row at zero units from `ProductCreatedEvent`, leaving an already-registered product untouched
- [X] T024 [P] Implement `SetStockOnHandCommand` + handler + validator in `.../Application/Stock/Commands/SetStockOnHand/`: absolute value not a delta, rejected with `ConflictException` if it would fall below `QuantityReserved`, taking the same row lock as the reserve path
- [X] T025 [P] Implement `GetStockByProductIdQuery` and `GetStockQuery` (paged) in `.../Application/Stock/Queries/`, returning `quantityAvailable` as a derived value
- [X] T026 Implement `StockController` in `.../WebApi/Controllers/StockController.cs` per [http-api.md](./contracts/http-api.md): `GET` endpoints `[AllowAnonymous]`, `PUT` `[Authorize(Roles = "Admin")]`. Add `ApiControllerBase` alongside it, as the other services have
- [X] T027 [P] Write consumer tests for product registration in `server/tests/Ecommerce.Inventory.Tests/Consumers/ProductCreatedConsumerTests.cs`: registers at zero; **a second delivery of the same event changes nothing** (FR-006)

**Checkpoint**: the service starts, `/health` returns 200, a product created in Catalog appears in
`GET /api/stock/{productId}` at zero units, and an admin can stock it. Quickstart scenario 1 passes.

---

## Phase 3: User Story 1 — A shopper's order is backed by real stock (P1) 🎯 MVP

**Goal**: A submitted order reserves stock and the saga leaves `Submitted`.

**Independent test**: Place an order for a well-stocked product; the order advances and available
quantity drops by the ordered amount (quickstart scenario 2).

### Tests for User Story 1

- [X] T028 [P] [US1] Write `ReserveInventoryConsumerTests.cs` in `server/tests/Ecommerce.Inventory.Tests/Consumers/` using `AddMassTransitTestHarness`: a reservable order publishes exactly one `InventoryReservedEvent` and no failure event (FR-002)
- [X] T029 [P] [US1] Write `ReserveIdempotencyTests.cs`: delivering the same `ReserveInventoryCommand` twice yields one reservation row, one reply, and stock moved once (SC-005)
- [X] T030 [P] [US1] Write `ReserveConcurrencyTests.cs` against a **real PostgreSQL**: 100 concurrent single-unit reservations against 10 units yield exactly 10 successes, `QuantityReserved` never exceeds 10, and every request gets an outcome (SC-004, SC-001). An in-memory provider would pass against broken code — constitution Principle V

### Implementation for User Story 1

- [X] T031 [US1] Implement `ReserveStockCommand` + handler in `.../Application/Reservations/ReserveStock/`: sum duplicate lines for the same product before evaluating, lock all needed stock rows in `ProductId` order, and reserve every line or none (FR-003)
- [X] T032 [US1] Implement `ReserveInventoryConsumer` in `.../WebApi/Consumers/ReserveInventoryConsumer.cs`: dispatch through MediatR, then **stage the reservation rows, publish the reply, and call `SaveChangesAsync` exactly once** — the ordering that constitution Principle III makes non-negotiable and that this repository has already got wrong twice
- [X] T033 [US1] Set `ExpiresAt` on each reservation from `INVENTORY_RESERVATION_TTL_MINUTES` at creation time in `.../Application/Reservations/ReserveStock/ReserveStockCommandHandler.cs`, so the row is ready for the sweeper added in US4
- [X] T034 [P] [US1] Implement `ConfirmStockCommand` + handler in `.../Application/Reservations/ConfirmStock/`: `Held → Confirmed`, reducing **both** `QuantityReserved` and `QuantityOnHand` — confirmed stock leaves the building rather than returning to the shelf
- [X] T035 [US1] Implement `OrderCompletedConsumer` in `.../WebApi/Consumers/OrderCompletedConsumer.cs`. This closes the gap found in [research D2](./research.md): the saga publishes `OrderCompletedEvent` and finalizes without telling inventory, so without this consumer every successful order's units stay held until the sweeper wrongly returns them — re-selling goods already shipped
- [X] T036 [P] [US1] Write `OrderCompletedConsumerTests.cs`: confirming reduces both counters; a second delivery changes nothing; confirming an unknown order is a no-op

**Checkpoint**: quickstart scenarios 2 and 5 pass. This is the MVP — orders stop getting stuck.

---

## Phase 4: User Story 2 — A shopper is told promptly that an item is unavailable (P2)

**Goal**: An unfulfillable order is rejected with a reason instead of hanging.

**Independent test**: Order more units than exist; the order fails with a reason naming the product
and stock is unchanged (quickstart scenario 3).

> Shares `ReserveInventoryConsumer` with US1 — this phase adds its rejection branch. The stories are
> independently *testable* but not independently *deployable*; stated here rather than implied.

### Tests for User Story 2

- [X] T037 [P] [US2] Write `ReserveRejectionTests.cs` covering each rejection reason from [messages.md](./contracts/messages.md): insufficient stock, unknown product, non-positive quantity, empty item list — each asserting exactly one `InventoryReservationFailedEvent` and unchanged stock
- [X] T038 [P] [US2] Write `ReserveAllOrNothingTests.cs`: an order with one available and one unavailable line reserves **neither** (FR-003)

### Implementation for User Story 2

- [X] T039 [US2] Extend the `ReserveStockCommand` handler to evaluate every line and collect the first failing condition into a reason string naming the offending product, per the reason table in [messages.md](./contracts/messages.md)
- [X] T040 [US2] Extend `.../WebApi/Consumers/ReserveInventoryConsumer.cs` to publish `InventoryReservationFailedEvent` on the rejection path, in the same single-transaction pattern as the success path
- [X] T041 [P] [US2] Add a validator rejecting non-positive quantities and empty item lists in `.../Application/Reservations/ReserveStock/ReserveStockCommandValidator.cs` (FR-009)

**Checkpoint**: quickstart scenario 3 passes. Orders now always reach a definite outcome (SC-001).

---

## Phase 5: User Story 3 — Stock returns to the shelf when an order falls through (P3)

**Goal**: A failed payment returns held units to available.

**Independent test**: Reserve, trigger payment failure, watch available return to its original
level (quickstart scenario 4).

### Tests for User Story 3

- [X] T042 [P] [US3] Write `ReleaseInventoryConsumerTests.cs`: release returns units and marks the reservation `Released` with the reason
- [X] T043 [P] [US3] Write `ReleaseIdempotencyTests.cs`: a second release credits nothing; a release for an order never reserved is a silent no-op; a release arriving *before* its reserve leaves stock correct once both are processed (spec edge cases)

### Implementation for User Story 3

- [X] T044 [US3] Implement `ReleaseStockCommand` + handler in `.../Application/Reservations/ReleaseStock/`: guarded `Held → Released` transition recording `SettledAt` and `SettlementReason`
- [X] T045 [US3] Implement `ReleaseInventoryConsumer` in `.../WebApi/Consumers/ReleaseInventoryConsumer.cs`. Publishes no reply — the saga does not wait on one

**Checkpoint**: quickstart scenario 4 passes. Failed payments no longer destroy sellable stock.

---

## Phase 6: User Story 4 — Stock is not lost when a checkout dies mid-flight (P4)

**Goal**: Stranded reservations recover without anyone intervening.

**Independent test**: Reserve, send neither settlement message, and watch stock return after the
holding period (quickstart scenario 6).

### Tests for User Story 4

- [X] T046 [P] [US4] Write `ExpirySweeperTests.cs`: a reservation past `ExpiresAt` is expired and its units returned; one inside its holding period is left alone
- [X] T047 [P] [US4] Write `LateSettlementTests.cs`: a release or confirm arriving **after** expiry changes nothing and raises no error (FR-017)

### Implementation for User Story 4

- [X] T048 [US4] Implement `ExpireStockCommand` + handler in `.../Application/Reservations/ExpireStock/`: guarded `Held → Expired`, batched, taking stock locks **in the same `ProductId` order as the reserve path** — a different order here makes the sweeper a deadlock source
- [X] T049 [US4] Implement `ReservationExpirySweeper` in `.../Infrastructure/BackgroundServices/ReservationExpirySweeper.cs` as a `BackgroundService` on a configurable interval, and register it in `Program.cs`
- [X] T050 [P] [US4] Bind the holding period and sweep interval from `INVENTORY_RESERVATION_TTL_MINUTES` in `.../WebApi/Program.cs` and an options class in `.../Infrastructure/BackgroundServices/ReservationExpiryOptions.cs`, defaulting to 15 minutes

**Checkpoint**: quickstart scenario 6 passes. No stranded stock needs a manual database edit (SC-007).

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T051 Implement `ReservationsController` (`GET /api/reservations/{orderId}`, `Admin` only) in `.../WebApi/Controllers/ReservationsController.cs` so a stuck order can be diagnosed without database access (FR-010)
- [X] T052 [P] Add an at-rest consistency assertion in `server/tests/Ecommerce.Inventory.Tests/AtRestConsistencyTests.cs`: with nothing in flight, `QuantityReserved == 0` and `QuantityAvailable == QuantityOnHand` for every product (SC-008). A violation means a transition leaked
- [X] T053 Add the `postgres-inventory` service container and the new test project to `.github/workflows/ci.yml`, so the concurrency and idempotency guarantees are enforced on every push rather than asserted once
- [X] T054 [P] Extend `.github/scripts/verify-auth.sh` or add a sibling script covering the inventory endpoints' authorization boundary: anonymous read allowed, anonymous write 401, customer write 403, admin write through
- [X] T055 [P] Update `docs/infrastructure/database-setup.md` with the inventory database row (port 5437) and its migration commands
- [X] T056 [P] Update `docs/README.md` service port table (Inventory 5060) and `CLAUDE.md` service map
- [X] T057 [P] Record in the Catalog documentation that `Product.StockQuantity` is now **descriptive only** and MUST NOT inform an availability decision. Without this note the next developer will reasonably assume it is authoritative — constitution Principle I ([research D5](./research.md))
- [X] T058 [P] Update `docs/architecture/saga-orchestration-roadmap.md`: the saga now progresses past `Submitted`, and record plainly that it still stops at `InventoryReservedState` until a payment service exists
- [X] T059 Run every scenario in `specs/001-inventory-reservations/quickstart.md` end to end against real services and record the results in that file, including the scenarios that need `PaymentProcessedEvent` published by hand

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — **blocks every user story**
- **US1 (Phase 3)**: depends on Foundational
- **US2 (Phase 4)**: depends on Foundational; touches the same consumer as US1, so it is sequenced after US1 rather than parallel to it
- **US3 (Phase 5)**: depends on Foundational. Independent of US1 and US2 in code — a different consumer and a different handler
- **US4 (Phase 6)**: depends on Foundational, and on T033 (`ExpiresAt` being set) from US1
- **Polish (Phase 7)**: depends on the stories being delivered

### Within Each User Story

- Tests are written before the implementation they describe and must fail first
- Domain before repositories, repositories before handlers, handlers before consumers
- The story is complete and its checkpoint verified before the next priority starts

### Parallel Opportunities

- T005–T008 in Setup — four separate files, no shared state
- T009, T010, T011 in Foundational — three separate entity files
- T013, T014 — two separate configuration files
- All test tasks within a story — separate test files
- **US3 can be built in parallel with US1/US2** by a second person: different consumer, different handler, only the shared repositories in common

### Parallel Example: Foundational

```text
Task: "Create StockItem in .../Domain/Entities/StockItem.cs"
Task: "Create StockReservation in .../Domain/Entities/StockReservation.cs"
Task: "Create ReservationStatus in .../Domain/Enums/ReservationStatus.cs"
```

---

## Implementation Strategy

### MVP scope

Phases 1–3 (T001–T036). That delivers User Story 1: an order reserves stock, the saga leaves
`Submitted`, and a completed order deducts permanently. It is the smallest increment that fixes the
defect this feature exists for — every order in the system currently being stuck forever.

### Incremental delivery

1. Setup + Foundational → service runs, products register, stock can be set
2. **+ US1 → MVP.** Orders stop getting stuck. Stop and validate
3. + US2 → unfulfillable orders fail cleanly instead of hanging
4. + US3 → failed payments stop destroying stock
5. + US4 → stranded stock recovers unattended
6. + Polish → CI enforces the guarantees, docs match the code

### What this does not finish

The saga still stops at `InventoryReservedState` — there is no payment service. Every reservation
will therefore be expired by the sweeper once US4 lands, which is correct behaviour and not a bug.
Exercising the confirm path before a payment service exists means publishing `PaymentProcessedEvent`
by hand (quickstart scenario 5).

---

## Implementation notes

Completed 2026-09-16. Deviations from the plan, recorded rather than left to be discovered:

- **Test files were consolidated.** The plan named eight separate test files; the suite is three
  (`ReserveStockTests`, `SettlementTests`, plus the shared `InventoryTestFixture`) holding 19 tests
  that cover every case those tasks listed. Splitting them further would have spread one fixture
  across eight files for no gain.
- **`IUnitOfWork` was added**, which the plan did not anticipate. `SELECT ... FOR UPDATE` holds its
  lock only for the life of a transaction, and EF opens a fresh one per `SaveChangesAsync`, so
  without an explicit transaction the lock was released before the decision based on it was written.
- **`INVENTORY_SWEEP_INTERVAL_SECONDS` was added** alongside the TTL setting, so expiry can be
  exercised without waiting fifteen minutes.
- **T059 was run by hand, not automated.** Quickstart scenarios 1-6 and 8 were exercised against
  real services; scenario 7 (concurrency) lives in the test suite instead, where it belongs.

## Notes

- 59 tasks: 8 setup, 19 foundational, 9 for US1, 5 for US2, 4 for US3, 5 for US4, 9 polish
- 13 test tasks, all of which assert a stated success criterion rather than an implementation detail
- Commit after each task or logical group, with a scoped Conventional Commit message
