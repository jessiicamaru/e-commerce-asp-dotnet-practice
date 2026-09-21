# Tasks: A Total With Something Behind It

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `012-order-totals`

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
- [ ] T015 PR `Closes #21`; CI green; squash-merge

## What actually happened

- **Backfill before constraints.** The migration gives existing orders the parts they were actually
  charged (tax 0, rate 0) *before* adding the CHECKs, so the constraints validate against rows that
  already satisfy them. On the local database: 70 orders, 0 without parts, 0 not summing.
- **Negative control (T011)**: `AwayFromZero` → `ToEven` fails 2 of 6 arithmetic tests; restored.
- **`verify-saga.sh` recomputes tax independently** from the stored rate with Python `Decimal`
  (`ROUND_HALF_UP`), rather than trusting Order's own figure: 29.97 + 15.00 + 3.00 + 1.50 = 49.47,
  and Payment was asked for 49.47.
- Tests 113/113 (Order 31 → 41). Bruno 50/51 — the one failure is still #28.
