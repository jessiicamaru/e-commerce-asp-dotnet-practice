# Tasks: Each seller ships their own part

**Tests**: requested - every acceptance item is a refusal or a race, and those are only proven by a test that fails without its guard.

## Phase 1: Foundational - parts exist

- [ ] T001 `ShipmentStatus` + `OrderShipment` in server/src/Services/Order/Ecommerce.Order.Domain/; `Order.Shipments` navigation
- [ ] T002 Configuration with the NULLS NOT DISTINCT unique index in server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Configurations/OrderShipmentConfiguration.cs
- [ ] T003 Migration `AddOrderShipments` with the backfill SQL
- [ ] T004 Checkout stages one part per distinct seller - server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommandHandler.cs

## Phase 2: US1 + US4 - moving a part (P1)

- [ ] T005 Tests first in server/tests/Ecommerce.Order.Tests/ShipmentTests.cs: a seller prepares and ships their part; repeating is a no-op; skipping and going back are refused; another seller's part and the shop's are 404; an unpaid or failed order is 404; two parts shipped concurrently leave the order Shipped; parts missing on an order written by an older version are created in the order's state
- [ ] T006 `TryMoveShipmentAsync` + `EnsureShipmentsAsync` on IOrderRepository / OrderRepository: order row lock, create-missing, guarded part update, summary recompute - one transaction
- [ ] T007 `PrepareMySaleCommand`, `ShipMySaleCommand` (+ validator) in Application/Orders/Commands/SellerFulfilment/
- [ ] T008 Admin commands move the shop's part (FulfilmentStep); 409 when the order has no shop part; queue reads the shop's part
- [ ] T009 Routes `POST /api/orders/sales/{id}/preparing` and `/shipment`, Seller only

## Phase 3: US2 + US3 - what each side reads (P1)

- [ ] T010 Tests: sale status/tracking from the seller's part; address while Pending/Preparing and not once Shipped; order detail lists parts; summary counts parts shipped; a one-part order reads as before
- [ ] T011 Sale reads, order detail `shipments`, summary `shipmentCount`/`shipmentsShipped`

## Phase 4: US5 - storefront (P2)

- [ ] T012 Services/hooks for the two seller moves; types for shipments
- [ ] T013 Seller sale page: next-step button, tracking dialog, address while needed
- [ ] T014 Customer order page parcels; orders list "1 of 2 shipped"
- [ ] T015 Strings (vi, en) and Vitest tests

## Phase 5: Polish

- [ ] T016 Bruno: seller moves a part that is not theirs → 404; customer → 403; anonymous → 401
- [ ] T017 Run everything; mutation check on the guards; end to end on Minh's two-seller order; verify-saga.sh
- [ ] T018 CLAUDE.md
