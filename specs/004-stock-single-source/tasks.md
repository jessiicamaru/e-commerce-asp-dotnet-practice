# Tasks: One Source of Truth for Stock

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `004-stock-single-source`

**Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests are included.** The constitution's Development Workflow names the categories this feature
falls in — idempotency, and behaviour that cannot be checked by hand. The six publish tests (US2)
are the most valuable in the feature and the easiest to skip once the happy path works.

**Organization**: by user story.

---

## Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

`[P]` = parallelizable (different file, no incomplete dependency).

---

## Phase 1: Setup

- [ ] T001 [P] Create `server/tests/Ecommerce.Catalog.Tests/Ecommerce.Catalog.Tests.csproj` mirroring `server/tests/Ecommerce.Order.Tests/Ecommerce.Order.Tests.csproj` — same package versions, project references to `Ecommerce.Catalog.Application`, `Ecommerce.Catalog.Infrastructure` and `Ecommerce.Catalog.WebApi`
- [ ] T002 Register the test project in `server/Ecommerce.slnx`
- [ ] T003 [P] Add a `postgres-catalog` service container on 5433 to the `build` job in `.github/workflows/ci.yml`, alongside the existing inventory/payment/order containers

---

## Phase 2: Foundational

**Blocks every user story.** The contract and the schema come first because both sides depend on them.

- [ ] T004 Add `StockAvailabilityChangedEvent(Guid ProductId, int QuantityAvailable, bool IsAvailable, DateTime ObservedAt)` to `server/src/BuildingBlocks/Ecommerce.Contracts/Inventory/`. Put it beside the existing reservation messages in `ReserveInventoryCommand.cs` or its own file, matching how that folder is already organised. **This is a breaking contract change** — see contracts/messages.md
- [ ] T005 Replace `StockQuantity` with `Availability` (bool, not null, default `false`) and `AvailabilityObservedAt` (nullable `timestamptz`) on `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Product.cs`
- [ ] T006 Map both columns in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` — Fluent API only. The `false` default is required by FR-005, not cosmetic
- [ ] T007 Generate the migration: `dotnet ef migrations add ReplaceProductStockQuantityWithAvailability --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/`. **Read the generated file before committing** — it must drop one column and add two, and touch nothing else
- [ ] T008 Add an announcement helper to `server/src/Services/Inventory/Ecommerce.Inventory.Application/Common/` that builds a `StockAvailabilityChangedEvent` from a `StockItem`. One implementation so six call sites cannot drift in what they send
- [ ] T009 Create `server/tests/Ecommerce.Catalog.Tests/CatalogTestFixture.cs` modelled on `OrderTestFixture` — throwaway database on 5433, `Maximum Pool Size=20` from the start, `AddMassTransitTestHarness` with the real consumer registered

**Checkpoint**: `dotnet build` succeeds; Catalog and Inventory both still start.

---

## Phase 3: User Story 1 — The listing stops claiming a number it cannot know (P1) 🎯 MVP

**Goal**: `availability` replaces `stockQuantity` on the product payload, fed by announcements.

**Independent test**: compare each product's availability against Inventory's own figure. They agree,
or nothing about stock is shown.

- [ ] T010 [US1] Add the guarded, time-compared update to `IProductRepository` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/` — returns rows affected. Make "zero rows is a normal answer" obvious at the call site
- [ ] T011 [US1] Implement it in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs` with `ExecuteUpdateAsync` and `Where(p => p.Id == id && (p.AvailabilityObservedAt == null || p.AvailabilityObservedAt < observedAt))`. **Both conditions belong in the query** — see data-model.md for the table of what each one catches
- [ ] T012 [US1] Add an existence check to the same interface and repository, so a zero-row result can be told apart from an announcement about a product this catalogue does not hold (FR-008)
- [ ] T013 [US1] Create `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Availability/RecordStockAvailabilityCommand.cs` and its handler — call the guarded update, and on zero rows log `Information` ("already current or overtaken") or `Warning` ("no such product here"), two distinguishable messages
- [ ] T014 [US1] Create `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Consumers/StockAvailabilityChangedConsumer.cs` — dispatch through `ISender` and nothing else
- [ ] T015 [US1] Wire Catalog's bus in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs`: `AddConsumer`, `AddConfigureEndpointsCallback` with `UseEntityFrameworkOutbox<CatalogDbContext>`, **and `SetEndpointNameFormatter(new DefaultEndpointNameFormatter(prefix: "CatalogSvc", includeNamespace: false))`**. The prefix is not optional — feature 003 shipped a defect from two services colliding on one queue name. **No migration**: `InboxState` has been in `20260902161405_AddMassTransitOutbox.cs` since September
- [ ] T016 [US1] Replace `StockQuantity` with `Availability` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Common/ProductResponse.cs`, serialised as a string (`"InStock"` / `"OutOfStock"`), matching how order status is already sent
- [ ] T017 [US1] Update the three query handlers that build `ProductResponse` — `GetProducts`, `GetProductById`, and the response built by `CreateProductCommandHandler`
- [ ] T018 [P] [US1] Test in `server/tests/Ecommerce.Catalog.Tests/AvailabilityTests.cs`: an announcement flips a product to available and records `ObservedAt`
- [ ] T019 [P] [US1] Test: a product with no announcement reads unavailable (FR-005, SC-007)
- [ ] T020 [P] [US1] Test: an announcement for a product this catalogue does not hold completes without throwing and without redelivery (FR-008)

**Checkpoint**: the listing reports availability from Inventory, and no stock number appears in any Catalog response.

---

## Phase 4: User Story 2 — Selling the last unit changes what shoppers see (P2)

**Goal**: every path that moves stock announces it.

**Independent test**: buy a product's remaining units through checkout and re-read the listing
without touching it by hand.

- [ ] T021 [US2] Publish from `ReserveStockCommandHandler.cs` — stage, publish, then the single `SaveChangesAsync`. It already has `IPublishEndpoint` and already publishes; add the announcement to the same transaction
- [ ] T022 [US2] Publish from `ReleaseStockCommandHandler.cs`
- [ ] T023 [US2] Publish from `ConfirmStockCommandHandler.cs` — note it writes inside `ExecuteInTransactionAsync`; the announcement goes inside the same unit of work
- [ ] T024 [US2] Publish from `ExpireStockCommandHandler.cs` — the sweeper path, the one most likely to be forgotten because no user triggers it
- [ ] T025 [US2] Publish from `SetStockOnHandCommandHandler.cs`
- [ ] T026 [US2] Publish from `RegisterProductCommandHandler.cs` — announcing `IsAvailable = false` for a new stock item at zero. Not redundant: it turns "never told" into "told, and the answer is no"
- [ ] T027 [P] [US2] Six tests in `server/tests/Ecommerce.Inventory.Tests/AnnouncementTests.cs`, one per handler above, asserting each publishes an announcement carrying the availability the row ends up with. **These are the most valuable tests in this feature** — six publish sites is six chances to forget one, and a forgotten one is invisible
- [ ] T028 [P] [US2] Test in `Ecommerce.Catalog.Tests`: ten deliveries of one announcement leave the row identical after the tenth and the first (FR-006, SC-004)
- [ ] T029 [P] [US2] Test: an announcement with an older `ObservedAt` delivered *after* a newer one does not overwrite it (FR-007, SC-005)
- [ ] T030 [US2] **Mutation-check T028 and T029 separately.** Remove the whole guard → T028 must fail. Restore it, then remove only the `AvailabilityObservedAt` comparison → T029 must fail while T028 still passes. That separation is the point: the two requirements look like one and are not. Record both observed failures in the PR

**Checkpoint**: a completed order flips the listing, and so does a release, an expiry and an admin adjustment.

---

## Phase 5: User Story 3 — Creating a product no longer asks for a number it cannot keep (P3)

**Goal**: the create-product request stops accepting a stock quantity.

**Independent test**: create a product and confirm no stock figure was recorded outside Inventory.

- [ ] T031 [US3] Remove `StockQuantity` from `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Commands/CreateProduct/CreateProductCommand.cs`
- [ ] T032 [US3] Remove its rule from `CreateProductCommandValidator.cs`
- [ ] T033 [US3] Remove the assignment from `CreateProductCommandHandler.cs:42` — the single place the column was ever written
- [ ] T034 [P] [US3] Test: a create request carrying `stockQuantity` is **rejected**, not silently accepted. An ignored field lets an administrator believe they set stock when they did not
- [ ] T035 [P] [US3] Test: a newly created product reads unavailable, and its response carries no stock number

**Checkpoint**: all of issue #4's acceptance bullets are demonstrable through HTTP.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T036 Run every scenario in [quickstart.md](./quickstart.md) against started services and paste the **real** output into the PR — not a claim that it passed. Constitution V
- [ ] T037 **Check the queues before trusting scenario 3**: `docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages consumers`. Every queue shows 1 consumer, and `CatalogSvcStockAvailabilityChanged` exists. Feature 003's collision passed all fifteen unit tests and only showed up here
- [ ] T038 [P] Update `CLAUDE.md` — the Catalog row in the service map, the shared-building-blocks section, and a line on who owns stock
- [ ] T039 [P] Update `docs/architecture/microservices-design.md`, which describes the current ownership
- [ ] T040 [P] Update `docs/architecture/saga-orchestration-roadmap.md` if it references the product stock figure
- [ ] T041 Run `dotnet build` and the full `dotnet test` with `DB_PASSWORD` set; report the real pass counts per project, including the three existing suites

---

## Dependencies

```text
Phase 1 (T001-T003)
   └─> Phase 2 (T004-T009)   ← contract + schema, blocks everything
          ├─> Phase 3 US1 (T010-T020)   ← MVP: the listing reports availability
          │      └─> Phase 4 US2 (T021-T030)  ← needs the consumer to exist to be observable
          └─> Phase 5 US3 (T031-T035)   ← independent of US1/US2 in code
                 └─> Phase 6 (T036-T041)
```

- **US2 depends on US1** in a way US3 does not: publishing announcements is only observable once
  something consumes them. The six publish tests (T027) can be written before US1 — they assert on
  the harness, not on Catalog.
- **US3 touches only Catalog's create path** and shares no file with US2.
- **T007's migration and T031's command change are the two breaking edges.** Nothing else changes a
  shape anyone depends on.

## Parallel opportunities

| Phase | Can run together |
| :--- | :--- |
| 1 | T001, T003 |
| 2 | T004 and T005/T006 (different services) |
| 3 | T018, T019, T020 |
| 4 | T021–T026 are six separate files; T027–T029 after them |
| 5 | T031–T033 are sequential (same feature folder); T034, T035 together |
| 6 | T038, T039, T040 |

## Implementation strategy

**MVP is Phase 1 + Phase 2 + Phase 3** — 20 tasks. At that point the listing reports availability
fed by Inventory, which closes the headline of issue #4. It is worth stopping there and running
quickstart scenarios 1 and 2 before starting Phase 4, because Phase 4 repeats the same publish shape
six times and a mistake in the first would be copied five more.

Then Phase 4 (which is what keeps the answer true as stock moves), then Phase 5, then Phase 6.

**Do not defer T027, T030 or T036.** They are the three tasks that turn "the code is written" into
evidence, and they are the three most likely to be dropped once the happy path works. T027 in
particular is the only thing standing between this feature and a forgotten publish site that nobody
notices until a shopper buys something that is gone.
