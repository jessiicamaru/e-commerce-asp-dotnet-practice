# Tasks: What the shop owes each seller

## Phase 1 - Foundation

- [X] T001 `Earnings.SplitDelivery` + `Earnings.ForPart` (pure) with unit tests - server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/Earnings.cs, server/tests/Ecommerce.Order.Tests/EarningsTests.cs
- [X] T002 `ICommissionRate` + `ConfiguredCommissionRate` (startup check), `Marketplace:CommissionRate` in appsettings and test fixture
- [X] T003 Domain: `Order.CommissionRate`; `OrderShipment.GoodsTotal/Commission/ShippingShare/PayoutId`; `Payout` entity; configurations (CHECK all-or-none, FK, indexes); migration `AddSellerPayouts`

## Phase 2 - US1 earnings per sale (P1)

- [X] T004 [US1] Tests red first: checkout freezes rate and part amounts; shares sum to delivery; a rate change leaves an existing sale unchanged; sale responses carry earnings; older parts read null - server/tests/Ecommerce.Order.Tests/PayoutTests.cs
- [X] T005 [US1] Checkout: `PricedCheckout.Decimals`; `SubmitOrderCommandHandler` writes the rate and each part's amounts
- [X] T006 [US1] `SaleSummaryResponse`/`SaleDetailResponse` gain `GoodsTotal`, `Commission`, `ShippingShare`, `Payout`, `PaidOut`

## Phase 3 - US2 balance and payouts for a seller (P1)

- [X] T007 [US2] Tests: on the way / due / paid out per currency; failed and settling orders count nowhere; payouts list is the caller's only
- [X] T008 [US2] `GetMyBalanceQuery`, `GetMyPayoutsQuery`; repository reads; routes `GET /sales/balance`, `/sales/payouts`

## Phase 4 - US3 administrator settles (P2)

- [X] T009 [US3] Tests: due list; payout covers every due part with the right amount; nothing due → 409 and no row; concurrent payouts claim each part once; `RecordedBy` from the token
- [X] T010 [US3] `GetPayoutsDueQuery`, `RecordPayoutCommand` (+ validator), `IPayoutRepository.TryRecordAsync` (research D5); routes `GET /payouts/due`, `POST /payouts` (Admin)

## Phase 5 - Client, Bruno, docs

- [X] T011 Client: types, services, hooks; `/shop/payouts` page (balance cards + history, `Pager`); earnings on the sale page; sidebar link; vi/en strings; Vitest tests
- [X] T012 Bruno: seller balance, seller payouts, admin due, nothing due → 409, seller → 403 and customer → 403 on the admin endpoints, 400, 401. (The 201 needs a shipped sale, which one collection run does not make: `PayoutTests` and the end to end prove it.)
- [X] T013 Run everything: Order tests, full server tests, client lint/test/build, Bruno, verify-saga.sh, end to end per quickstart
- [X] T014 CLAUDE.md (Order row + paragraph), docs as needed

> **Tests first on the server, mostly.** All fifteen `PayoutTests` were written before the code; ten
> were seen RED against a repository that returned nothing and a checkout that recorded no
> terms. `EarningsTests` was written alongside `Earnings`. The five `PayoutTests` that passed against that stub pass vacuously there, so every guard was then removed
> one at a time (paid-status filter on read and on claim, `PayoutId IS NULL`, terms recorded, shipped,
> currency, rounding, remainder, the live rate, the shop's zero commission): all ten mutations turned a
> test red. The client code preceded its tests; six mutations of it each turned one red. End to end on
> the demo data: Lan's two-seller order split 30,000 ₫ as 15,000 each, 10% commission, on the way →
> due → Mai paid out (201), paid again (409), Mai paying herself (403), Tuấn left due.
