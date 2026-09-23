# Tasks: What the shop owes each seller

## Phase 1 - Foundation

- [ ] T001 `DeliverySplit.Split` + `PartEarnings` (pure) with unit tests first - server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/Earnings.cs, server/tests/Ecommerce.Order.Tests/EarningsTests.cs
- [ ] T002 `ICommissionRate` + `ConfiguredCommission` (startup check), `Marketplace:CommissionRate` in appsettings and test fixture
- [ ] T003 Domain: `Order.CommissionRate`; `OrderShipment.GoodsTotal/Commission/ShippingShare/PayoutId`; `Payout` entity; configurations (CHECK all-or-none, FK, indexes); migration `AddSellerPayouts`

## Phase 2 - US1 earnings per sale (P1)

- [ ] T004 [US1] Tests red first: checkout freezes rate and part amounts; shares sum to delivery; a rate change leaves an existing sale unchanged; sale responses carry earnings; older parts read null - server/tests/Ecommerce.Order.Tests/PayoutTests.cs
- [ ] T005 [US1] Checkout: `PricedCheckout.Decimals`; `SubmitOrderCommandHandler` writes the rate and each part's amounts
- [ ] T006 [US1] `SaleSummaryResponse`/`SaleDetailResponse` gain `GoodsTotal`, `Commission`, `ShippingShare`, `Payout`, `PaidOut`

## Phase 3 - US2 balance and payouts for a seller (P1)

- [ ] T007 [US2] Tests: on the way / due / paid out per currency; failed and settling orders count nowhere; payouts list is the caller's only
- [ ] T008 [US2] `GetMyBalanceQuery`, `GetMyPayoutsQuery`; repository reads; routes `GET /sales/balance`, `/sales/payouts`

## Phase 4 - US3 administrator settles (P2)

- [ ] T009 [US3] Tests: due list; payout covers every due part with the right amount; nothing due → 409 and no row; concurrent payouts claim each part once; `RecordedBy` from the token
- [ ] T010 [US3] `GetPayoutsDueQuery`, `RecordPayoutCommand` (+ validator), `TryRecordPayoutAsync` (research D5); routes `GET /payouts/due`, `POST /payouts` (Admin)

## Phase 5 - Client, Bruno, docs

- [ ] T011 Client: types, services, hooks; `/shop/payouts` page (balance cards + history, `Pager`); earnings on the sale page; sidebar link; vi/en strings; Vitest tests
- [ ] T012 Bruno: seller balance, seller payouts, admin due, admin payout 201, repeat 409, seller → 403 on admin endpoint
- [ ] T013 Run everything: Order tests, full server tests, client lint/test/build, Bruno, verify-saga.sh, end to end per quickstart
- [ ] T014 CLAUDE.md (Order row + paragraph), docs as needed
