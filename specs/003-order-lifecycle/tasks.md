# Tasks: Order Lifecycle Visibility

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `003-order-lifecycle`

**Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests are included.** Not because the spec asked in those words, but because the constitution's
Development Workflow section names the two categories this feature falls in: *"Concurrency,
idempotency, and authorization boundaries"* require an automated check by definition. Both are
central here.

**Organization**: by user story, so each phase is a slice that can be finished, demonstrated and
left alone.

---

## Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

`[P]` = parallelizable (different file, no incomplete dependency).

---

## Phase 1: Setup

- [ ] T001 [P] Create `server/tests/Ecommerce.Order.Tests/Ecommerce.Order.Tests.csproj` mirroring `server/tests/Ecommerce.Payment.Tests/Ecommerce.Payment.Tests.csproj` — same package versions, project references to `Ecommerce.Order.Application` and `Ecommerce.Order.Infrastructure`, plus `Ecommerce.Order.WebApi` for the consumers
- [ ] T002 Register the test project in `server/Ecommerce.slnx`
- [ ] T003 [P] Add a `postgres-order` service container on 5434 to the `build` job in `.github/workflows/ci.yml`, alongside the existing `postgres-inventory` and `postgres-payment`

---

## Phase 2: Foundational

**Blocks every user story.** Nothing in Phase 3 onwards compiles or runs without these.

- [ ] T004 Declare the guarded settle on `IOrderRepository` in `server/src/Services/Order/Ecommerce.Order.Application/Common/Interfaces/IOrderRepository.cs` — a method returning the number of rows affected, taking the order id, the target status, the optional failure reason and the timestamp. The signature must make "zero rows is a normal answer" obvious at the call site
- [ ] T005 Implement it in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs` with `ExecuteUpdateAsync` and `Status == OrderStatus.Submitted` in the `Where`. **The guard belongs in the query, not in a preceding `if`** — research D1 explains why the obvious shape is wrong
- [ ] T006 Add an existence check to the same interface and repository, so a zero-row result can be told apart from an order this service does not hold (research D2)
- [ ] T007 Wire the inbox in `server/src/Services/Order/Ecommerce.Order.WebApi/Program.cs`: `x.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<OrderDbContext>(context));`. **No migration** — `InboxState` and `OutboxState` are already in `20260903142425_InitialOrderSchema.cs`
- [ ] T008 Create `server/tests/Ecommerce.Order.Tests/OrderTestFixture.cs` modelled on `PaymentTestFixture` — a throwaway database on 5434, `Maximum Pool Size=20` from the start, `AddMassTransitTestHarness`, and a substitutable `ICurrentUser` so the query tests can name a caller

**Checkpoint**: `dotnet build` succeeds and the Order service still starts and still submits an order.

---

## Phase 3: User Story 1 — A finished order says it finished (P1) 🎯 MVP

**Goal**: an order whose checkout completes reads `Completed`.

**Independent test**: submit an order, let the saga finish, read the row. Nothing from US2 or US3 is
needed.

- [ ] T009 [US1] Create `server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/CompleteOrder/CompleteOrderCommand.cs` — `record CompleteOrderCommand(Guid OrderId, DateTime CompletedAt) : IRequest<bool>`
- [ ] T010 [US1] Create `CompleteOrderCommandHandler.cs` in the same folder: call the guarded settle, and on zero rows use T006 to log `Information` ("already settled") or `Warning` ("no such order here") — two distinguishable messages, per research D2
- [ ] T011 [US1] Create `server/src/Services/Order/Ecommerce.Order.WebApi/Consumers/OrderCompletedConsumer.cs` — dispatch through `ISender` and nothing else, matching `Ecommerce.Inventory.WebApi/Consumers/OrderCompletedConsumer.cs`. Record in a comment that Inventory subscribes to this same event and still receives it
- [ ] T012 [US1] Register `x.AddConsumer<OrderCompletedConsumer>();` in `Program.cs`
- [ ] T013 [P] [US1] Test in `server/tests/Ecommerce.Order.Tests/SettlementTests.cs`: a `Submitted` order receiving `OrderCompletedEvent` becomes `Completed` with `UpdatedAt` set from the event
- [ ] T014 [P] [US1] Test in the same file: ten deliveries of the same event produce one `Completed` order whose fields after the tenth equal its fields after the first (SC-003)
- [ ] T015 [US1] **Mutation-check T014**: delete `AND Status == Submitted` from T005, run T014, confirm it **fails**, restore the guard, confirm it passes. Record the observed failure in the PR. A test that passes either way is not testing the guarantee

**Checkpoint**: an order placed end to end reads `Completed`. This alone closes the headline of issue #2.

---

## Phase 4: User Story 2 — A failed order says why (P2)

**Goal**: both failure branches settle the order and record a reason.

**Independent test**: set `PAYMENT_OUTCOME=Reject`, submit, read the row. Separately, over-order to
exercise the reservation branch.

- [ ] T016 [US2] Create `server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/FailOrder/FailOrderCommand.cs` — `record FailOrderCommand(Guid OrderId, string Reason, DateTime FailedAt) : IRequest<bool>`
- [ ] T017 [US2] Create `FailOrderCommandHandler.cs`: same guarded settle, writing `FailureReason`. **Truncate to 512 characters** — `FailureReason` is `varchar(512)` and the contract's `Reason` is unbounded; a settlement that fails because the explanation was long is worse than a clipped explanation (contracts/messages.md)
- [ ] T018 [US2] Create `server/src/Services/Order/Ecommerce.Order.WebApi/Consumers/OrderFailedConsumer.cs`
- [ ] T019 [US2] Register `x.AddConsumer<OrderFailedConsumer>();` in `Program.cs`
- [ ] T020 [P] [US2] Test: `OrderFailedEvent` moves a `Submitted` order to `Failed` and stores the reason verbatim
- [ ] T021 [P] [US2] Test: a repeated `OrderFailedEvent` leaves the original reason and timestamp untouched (FR-004)
- [ ] T022 [P] [US2] Test: `OrderFailedEvent` for an order already `Completed` leaves it `Completed` with a null reason (FR-005)
- [ ] T023 [P] [US2] Test: either event for an order id this database does not hold completes without throwing and without redelivery (FR-006)

**Checkpoint**: both failure branches produce a readable outcome; the compensation path still returns the stock.

---

## Phase 5: User Story 3 — A shopper can look at their own orders (P3)

**Goal**: the status becomes visible without database access.

**Independent test**: two accounts, each sees only their own, in the list and by id.

- [ ] T024 [P] [US3] Create `server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/` with the shared response records — a summary row, a detail record with items, and a paged wrapper carrying `Page`, `PageSize`, `TotalCount`. Today `OrderResponse` lives beside `SubmitOrderCommand`, so nothing can reference it without importing a command namespace
- [ ] T025 [US3] Add the index to `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Configurations/OrderConfiguration.cs`: `HasIndex(x => new { x.UserId, x.CreatedAt }).IsDescending(false, true)` (data-model.md)
- [ ] T026 [US3] Generate the migration: `dotnet ef migrations add AddOrdersUserCreatedAtIndex --project src/Services/Order/Ecommerce.Order.Infrastructure/ --startup-project src/Services/Order/Ecommerce.Order.WebApi/`. Read the generated file before committing — it must contain the index and nothing else
- [ ] T027 [US3] Declare the two reads on `IOrderRepository`: a paged, owner-scoped list returning items and a total; and an owner-scoped single order with its items
- [ ] T028 [US3] Implement both in `OrderRepository.cs`. The owner goes **in the `Where` clause of the single-order query**, not in a check after loading — research D5. Use `AsNoTracking()` for both
- [ ] T029 [P] [US3] Create `Orders/Queries/GetMyOrders/GetMyOrdersQuery.cs` and `GetMyOrdersQueryValidator.cs` — `page` ≥ 1, `pageSize` between 1 and 100. Reject out-of-range rather than clamping (contracts/http-api.md)
- [ ] T030 [US3] Create `GetMyOrdersQueryHandler.cs` reading `ICurrentUser.Id`. **The query record must carry no user id** — constitution IV
- [ ] T031 [P] [US3] Create `Orders/Queries/GetMyOrderById/GetMyOrderByIdQuery.cs` — the order id only
- [ ] T032 [US3] Create `GetMyOrderByIdQueryHandler.cs` throwing `Ecommerce.Shared.Exceptions.NotFoundException` when the owner-scoped query finds nothing, so `GlobalExceptionHandler` renders a 404
- [ ] T033 [US3] Add `[HttpGet]` and `[HttpGet("{id:guid}")]` to `server/src/Services/Order/Ecommerce.Order.WebApi/Controllers/OrdersController.cs` — `Mediator.Send` and nothing else, class-level `[Authorize]` already covers them
- [ ] T034 [P] [US3] Test in `server/tests/Ecommerce.Order.Tests/OrderQueryTests.cs`: with orders belonging to two users, the list returns only the caller's, newest first
- [ ] T035 [P] [US3] Test: asking for another user's order id raises `NotFoundException`, **not** a forbidden/authorization error. Assert the exception type — this is the FR-010 disclosure rule and a 403 would pass a weaker assertion
- [ ] T036 [P] [US3] Test: paging returns the right slice and an honest `TotalCount`; a page past the end is empty rather than an error
- [ ] T037 [P] [US3] Test: the validator rejects `pageSize` of 5000 and `page` of 0

**Checkpoint**: all of issue #2's four acceptance bullets are demonstrable through HTTP.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T038 Run every scenario in [quickstart.md](./quickstart.md) against started services and paste the **real** output into the PR — not a claim that it passed. Constitution V
- [ ] T039 Verify `GET http://localhost:5000/api/orders` through the gateway. `order-route` matches `/api/orders/{**catch-all}` and the bare path has never been exercised there. If it 404s, add a route for the bare path — do not change the endpoint
- [ ] T040 [P] Update `CLAUDE.md`: the Saga section still says Inventory is the only consumer of `OrderCompletedEvent`, and the Order row in the service map should stop implying the service only publishes
- [ ] T041 [P] Update `docs/architecture/saga-orchestration-roadmap.md` — this closes a gap the roadmap describes
- [ ] T042 [P] Close the gap named in the plan's Constitution Check: add an order-list assertion to `.github/scripts/verify-auth.sh` and the Order service to the `auth-smoke` job in `.github/workflows/ci.yml`, so the token → `ICurrentUser` → owner-filter path is exercised with a **real signed token**. Until this is done, that path is verified only against a substituted identity — the same shape as the role-claim incident the constitution cites
- [ ] T043 Run `dotnet build` and the full `dotnet test` with `DB_PASSWORD` set; report the real pass counts per project, including the two existing suites

---

## Dependencies

```text
Phase 1 (T001-T003)
   └─> Phase 2 (T004-T008)   ← blocks everything
          ├─> Phase 3 US1 (T009-T015)   ← MVP
          ├─> Phase 4 US2 (T016-T023)   ← needs T004/T005; T022 also needs US1
          └─> Phase 5 US3 (T024-T037)   ← independent of US1/US2 in code,
                                           but only meaningful once they are done
                 └─> Phase 6 (T038-T043)
```

- **US1 and US2 share the guarded settle** from Phase 2, so the second is largely a repeat of the first with one extra column.
- **US3 touches no file that US1 or US2 touches**, except `OrdersController.cs` and `Program.cs`. It can be built in parallel by a second person.
- **T022 is the one cross-story test** — it needs both consumers registered.

## Parallel opportunities

| Phase | Can run together |
| :--- | :--- |
| 1 | T001, T003 |
| 3 | T013, T014 (same file, different methods — write both, run once) |
| 4 | T020, T021, T022, T023 |
| 5 | T024, T029, T031; then T034–T037 |
| 6 | T040, T041, T042 |

## Implementation strategy

**MVP is Phase 1 + Phase 2 + Phase 3** — 15 tasks. At that point the headline of issue #2 is closed:
orders that complete say so. It is worth stopping and confirming that against a real order before
starting Phase 4, because everything after it reuses the same guarded settle, and a mistake there
would be copied three more times.

Then Phase 4 (failure is the case that matters most operationally), then Phase 5 (which is what makes
any of it visible to a shopper), then Phase 6.

**Do not defer T015 or T038.** They are the two tasks that turn "the code is written" into evidence,
and they are the two most likely to be dropped when the rest is working.
