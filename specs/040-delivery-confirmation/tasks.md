# Tasks: Confirming a parcel arrived

- [ ] T001 Order: columns + migration `AddShipmentDelivery` (backfill ShippedAt); `ShippedAt` written on ship
- [ ] T002 [US1] Tests first: confirm a shipped parcel; not shipped → 409; repeat no-op; someone else's → 404 - server/tests/Ecommerce.Order.Tests/DeliveryTests.cs
- [ ] T003 [US1] `TryConfirmDeliveryAsync`, `ConfirmDeliveryCommand`, route; `ShipmentResponse` + id/deliveredAt/by
- [ ] T004 [US2] Tests: shipped-not-delivered is on the way, delivered is due, payout claims delivered only; PayoutTests' helper delivers
- [ ] T005 [US2] PayoutRepository requires DeliveredAt; `SaleDetailResponse.DeliveredAt`
- [ ] T006 [US3] Tests: sweep delivers only parcels shipped before the cutoff, as "Auto"; twice → once; options validation
- [ ] T007 [US3] `AutoConfirmDeliveriesCommand`, `DeliveryOptions`, `DeliveryConfirmationSweeper`, appsettings
- [ ] T008 Client: receive button per parcel and on a one-parcel order; received badges; order delivered; seller sees received; earnings; vi/en; Vitest
- [ ] T009 Bruno (received 200, repeat 200, someone else's 404, not shipped 409); verify-saga confirms the shipped parcel
- [ ] T010 Run everything; mutation checks; CLAUDE.md
