# Tasks: Confirming a parcel arrived

- [X] T001 Order: columns + migration `AddShipmentDelivery` (backfill ShippedAt); `ShippedAt` written on ship
- [X] T002 [US1] Tests first: confirm a shipped parcel; not shipped → 409; repeat no-op; someone else's → 404 - server/tests/Ecommerce.Order.Tests/DeliveryTests.cs
- [X] T003 [US1] `TryConfirmDeliveryAsync`, `ConfirmDeliveryCommand`, route; `ShipmentResponse` + id/deliveredAt/by
- [X] T004 [US2] Tests: shipped-not-delivered is on the way, delivered is due, payout claims delivered only; PayoutTests' helper delivers
- [X] T005 [US2] PayoutRepository requires DeliveredAt; `SaleDetailResponse.DeliveredAt`
- [X] T006 [US3] Tests: sweep delivers only parcels shipped before the cutoff, as "Auto"; twice → once; options validation
- [X] T007 [US3] `AutoConfirmDeliveriesCommand`, `DeliveryOptions`, `DeliveryConfirmationSweeper`, appsettings
- [X] T008 Client: receive button per parcel and on a one-parcel order; received badges; order delivered; seller sees received; earnings; vi/en; Vitest
- [X] T009 Bruno (received 200, repeat 200, someone else's 404 - "not shipped" 409 is DeliveryTests', as one collection run ships only one order); verify-saga confirms the shipped parcel
- [X] T010 Run everything; mutation checks; CLAUDE.md

> Tests first this time on the server: the delivery commands, routes and responses were stubbed, and
> seven of the twelve `DeliveryTests` were seen RED (the five green were the shipped-time write and the
> startup validation, written just before, and a "not found" the stub answered vacuously). Eight guards
> were then removed one at a time - the due and claim filters, owner, shipped, repeat, the sweep window,
> the sweep overwriting a customer, the shipped time - and each turned a test red. `PayoutTests`' helper
> now delivers as well as ships: six of its tests went red on the semantic change, which was the point.
> The client code preceded its tests; six mutations each turned one red. End to end: verify-saga.sh
> confirms the parcel it ships; Bruno 121/121; Lan confirmed a parcel in the browser.
