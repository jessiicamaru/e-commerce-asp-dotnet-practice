# Tasks: A Total With Something Behind It

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `012-order-totals`

## Phase 1: Foundational

- [ ] T001 Order Domain: `Order.Subtotal`, `TaxTotal`, `DiscountTotal`, `TaxRate` (nullable); `OrderItem.TaxAmount` (nullable)
- [ ] T002 `OrderConfiguration` / `OrderItemConfiguration`: precision (18,2), rate (5,4); CHECK sum (with `IS NULL` escape), CHECK discount = 0, CHECK 0 ≤ rate < 1
- [ ] T003 Migration `AddOrderTotals`: columns + checks + backfill existing rows (research D4); confirm additive

## Phase 2: US1–US3 — the total, taxed by destination, charged exactly (P1)

- [ ] T004 [US2] `ITaxRates` (Application) + `ConfiguredTaxRates` (Infrastructure) from `Tax:DefaultRate` / `Tax:Rates`, validated at startup; Order `appsettings.json` defaults (VN 0.10, GB 0.20, DE 0.19, US 0.00, default 0.10)
- [ ] T005 [US1] `OrderTotals.Compute(lines, deliveryPrice, rate)` — pure; per-line and delivery tax rounded `AwayFromZero`; returns subtotal, per-line tax, tax, discount 0, total
- [ ] T006 [US1] `SubmitOrderCommandHandler`: rate from the destination country; store all parts, per-line tax and the rate; `TotalAmount` = grand total (what the event carries)
- [ ] T007 [US1] Responses: `subtotal`, `shippingPrice`, `taxTotal`, `discountTotal`, `taxRate`, `totalAmount`; line `taxAmount`

## Phase 3: US4 — written down, enforced (P2)

- [ ] T008 [US4] `docs/architecture/adr-002-tax-exclusive-prices.md`
- [ ] T009 [US4] Tests: `OrderTotalsTests` (half-cent rounds away from zero; zero rate; three lines; parts sum); DB test — a row whose parts do not sum is refused
- [ ] T010 [US2] Test: same cart to two countries → equal subtotal, different tax; unlisted country → default rate
- [ ] T011 Negative control: remove `AwayFromZero` → half-cent test fails; restore

## Phase 4: Integration & polish

- [ ] T012 `verify-saga.sh`: recompute tax from the stored rate with the same rule; assert parts sum, subtotal = price × qty, and payment amount = stored total
- [ ] T013 Bruno: checkout asserts the parts sum
- [ ] T014 Docs: CLAUDE.md, microservices-design (Order), getting-started, README index (ADR-002)
- [ ] T015 PR `Closes #21`; CI green; squash-merge
