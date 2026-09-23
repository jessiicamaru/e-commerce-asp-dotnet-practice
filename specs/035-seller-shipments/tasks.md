# Tasks: Each seller ships their own part

**Tests**: requested - every acceptance item is a refusal or a race, and those are only proven by a test that fails without its guard.

## Phase 1: Foundational - parts exist

- [X] T001 `ShipmentStatus` + `OrderShipment` in server/src/Services/Order/Ecommerce.Order.Domain/; `Order.Shipments` navigation
- [X] T002 Configuration with the NULLS NOT DISTINCT unique index in server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Configurations/OrderShipmentConfiguration.cs
- [X] T003 Migration `AddOrderShipments` with the backfill SQL
- [X] T004 Checkout stages one part per distinct seller - server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommandHandler.cs

## Phase 2: US1 + US4 - moving a part (P1)

- [X] T005 Tests first in server/tests/Ecommerce.Order.Tests/ShipmentTests.cs: a seller prepares and ships their part; repeating is a no-op; skipping and going back are refused; another seller's part and the shop's are 404; an unpaid or failed order is 404; two parts shipped concurrently leave the order Shipped; parts missing on an order written by an older version are created in the order's state
- [X] T006 `TryMoveShipmentAsync` + `EnsureShipmentsAsync` on IOrderRepository / OrderRepository: order row lock, create-missing, guarded part update, summary recompute - one transaction
- [X] T007 `PrepareMySaleCommand`, `ShipMySaleCommand` (+ validator) in Application/Orders/Commands/SellerFulfilment/
- [X] T008 Admin commands move the shop's part (FulfilmentStep); 409 when the order has no shop part; queue reads the shop's part
- [X] T009 Routes `POST /api/orders/sales/{id}/preparing` and `/shipment`, Seller only

## Phase 3: US2 + US3 - what each side reads (P1)

- [X] T010 Tests: sale status/tracking from the seller's part; address while Pending/Preparing and not once Shipped; order detail lists parts; summary counts parts shipped; a one-part order reads as before
- [X] T011 Sale reads, order detail `shipments`, summary `shipmentCount`/`shipmentsShipped`

## Phase 4: US5 - storefront (P2)

- [X] T012 Services/hooks for the two seller moves; types for shipments
- [X] T013 Seller sale page: next-step button, tracking dialog, address while needed
- [X] T014 Customer order page parcels; orders list "1 of 2 shipped"
- [X] T015 Strings (vi, en) and Vitest tests

## Phase 5: Polish

- [X] T016 Bruno: seller moves a part that is not theirs → 404; customer → 403; anonymous → 401
- [X] T017 Run everything; mutation check on the guards; end to end on Minh's two-seller order; verify-saga.sh
- [X] T018 CLAUDE.md

> **What "tests first" actually was here.** The code was written before `ShipmentTests`, so the first
> green run proved nothing. A mutation run removed seven guards one at a time - the order row lock, the
> seller on the part guard, the address cut-off, "shipped when ALL parts are", the paid check, the
> order's state for missing parts, the single-parcel tracking copy - and **every one turned at least one
> test red**. The lock's removal is caught by the concurrency test, which is the one that matters.
>
> **Found on the way, not planned:** the first version of the checkout test sorted two v7 Guids made
> in the same millisecond and failed 3 runs in 6 on untouched code; it now compares sets. And the
> client suite, now 104 tests, hit Vitest's 5s timeout on different form-typing tests on different
> runs ("Test timed out", never an assertion); `testTimeout` is 15s, with the reason in the config.
