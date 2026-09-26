# Tasks: A Total With Something Behind It

> Completed on 2026-09-27, after the feature merged (#32), from the code at that merge, the pull
> request and docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `012-order-totals`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included - constitution principle V, and the spec's SC-005 asks for the half-cent case to
have an automated test.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 (the parts), US2 (by destination), US3 (charged exactly), US4 (written down, enforced)

Paths are under `server/src/Services/Order/` unless given in full.

## Phase 1: Foundational

- [X] T001 Order Domain: `Order.Subtotal`, `TaxTotal`, `DiscountTotal`, `TaxRate` (nullable); `OrderItem.TaxAmount` (nullable)
- [X] T002 `OrderConfiguration` / `OrderItemConfiguration`: precision (18,2), rate (5,4); CHECK sum (with `IS NULL` escape), CHECK discount = 0, CHECK 0 ≤ rate < 1
- [X] T003 Migration `AddOrderTotals`: columns + checks + backfill existing rows (research D4); confirm additive

## Phase 2: US1–US3 — the total, taxed by destination, charged exactly (P1)

- [X] T004 [US2] `ITaxRates` (Application) + `ConfiguredTaxRates` (Infrastructure) from `Tax:DefaultRate` / `Tax:Rates`, validated at startup; Order `appsettings.json` defaults (VN 0.10, GB 0.20, DE 0.19, US 0.00, default 0.10)
- [X] T005 [US1] `OrderTotals.Compute(lines, deliveryPrice, rate)` — pure; per-line and delivery tax rounded `AwayFromZero`; returns subtotal, per-line tax, tax, discount 0, total
- [X] T006 [US1] `SubmitOrderCommandHandler`: rate from the destination country; store all parts, per-line tax and the rate; `TotalAmount` = grand total (what the event carries)
- [X] T007 [US1] Responses: `subtotal`, `shippingPrice`, `taxTotal`, `discountTotal`, `taxRate`, `totalAmount`; line `taxAmount`

## Phase 3: US4 — written down, enforced (P2)

- [X] T008 [US4] `docs/architecture/adr-002-tax-exclusive-prices.md`
- [X] T009 [US4] Tests: `OrderTotalsTests` (half-cent rounds away from zero; zero rate; three lines; parts sum); DB test — a row whose parts do not sum is refused
- [X] T010 [US2] Test: same cart to two countries → equal subtotal, different tax; unlisted country → default rate
- [X] T011 Negative control: remove `AwayFromZero` → half-cent test fails; restore

## Phase 4: Integration & polish

- [X] T012 `verify-saga.sh`: recompute tax from the stored rate with the same rule; assert parts sum, subtotal = price × qty, and payment amount = stored total
- [X] T013 Bruno: checkout asserts the parts sum
- [X] T014 Docs: CLAUDE.md, microservices-design (Order), getting-started, README index (ADR-002)

## Phase 5: Recorded after the merge

Work the pull request did that the list above did not name. Added on 2026-09-27 from the diff of #32.

- [X] T016 [US3] Resolve `ITaxRates` right after `builder.Build()` in `Ecommerce.Order.WebApi/Program.cs`, beside `IShippingOptions`, so a bad rate stops the service at startup rather than failing each checkout; register it as a singleton in `Ecommerce.Order.Infrastructure/DependencyInjection.cs`
- [X] T017 [US1] Map the parts onto the read side: `OrderMapping.ToDetail` in `Application/Orders/Common/OrderMapping.cs` and `OrderDetailResponse` / `OrderItemDetailResponse` in `OrderResponses.cs`, so `GET /api/orders/{id}` returns what checkout returned
- [X] T018 [P] [US2] Give the test fixture rates in `server/tests/Ecommerce.Order.Tests/OrderTestFixture.cs` (default 0.10; VN 0.10, GB 0.20, US 0.00) and register `ConfiguredTaxRates`
- [X] T019 [P] [US3] Update `server/tests/Ecommerce.Order.Tests/CheckoutShippingTests.cs`: the checkout total, the stored row and the published `OrderSubmittedEvent.TotalAmount` are now 49.47 (29.97 + 15.00 + 3.00 + 1.50), not 44.97
- [X] T020 [P] [US3] `TotalsPersistenceTests.Stored_parts_sum_to_the_stored_total_and_each_line_keeps_its_tax` in `server/tests/Ecommerce.Order.Tests/TotalsPersistenceTests.cs`
- [X] T021 Update `docs/architecture/saga-orchestration-roadmap.md` (the pull request lists it; the original T014 did not)
- [X] T015 PR [#32](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/32) `Closes #21`; CI green; squash-merged as `20242f2` on 2026-09-22

## Dependencies & Execution Order

- **Phase 1** blocks everything: the columns and constraints must exist before the handler writes them.
- **T004 and T005** are independent of each other (an interface and a pure function); **T006** needs both.
- **T007 and T017** follow T006 - they return what it stored.
- **Phase 3** needs T005 (the function under test) and T003 (the constraint under test).
- **T012** needs a running stack with T006 deployed.

## What actually happened

- **Backfill before constraints.** The migration gives existing orders the parts they were actually
  charged (tax 0, rate 0) *before* adding the CHECKs, so the constraints validate against rows that
  already satisfy them. On the local database: 70 orders, 0 without parts, 0 not summing.
- **Negative control (T011)**: `AwayFromZero` → `ToEven` fails 2 of 6 arithmetic tests; restored.
- **`verify-saga.sh` recomputes tax independently** from the stored rate with Python `Decimal`
  (`ROUND_HALF_UP`), rather than trusting Order's own figure: 29.97 + 15.00 + 3.00 + 1.50 = 49.47,
  and Payment was asked for 49.47.
- Tests 113/113 (Order 31 → 41). Bruno 50/51 — the one failure is still #28.

## Notes

- **T015 is listed last although its id is lower.** It was the last task of the record written at the
  merge; the tasks added in Phase 5 describe work already inside that pull request, so they are
  numbered after it but belong before it.
- 21 tasks, all done. 7 of them are test tasks (T009, T010, T011, T012, T013, T019, T020).
