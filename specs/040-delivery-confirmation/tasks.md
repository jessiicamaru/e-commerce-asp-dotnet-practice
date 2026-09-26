---
description: "Task list for Confirming a parcel arrived"
---

# Tasks: Confirming a parcel arrived

> Completed on 2026-09-27, after the feature merged (#85), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. Story labels and paths were added to T001 and T008-T010;
> T011 onward were added in this backfill.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel; **[Story]**: US1 the customer confirms, US2 paid for what arrived,
  US3 nobody waits forever

- [X] T001 [US3] Order: columns + migration `AddShipmentDelivery` (backfill ShippedAt); `ShippedAt` written on ship - server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/20260923183204_AddShipmentDelivery.cs
- [X] T002 [US1] Tests first: confirm a shipped parcel; not shipped → 409; repeat no-op; someone else's → 404 - server/tests/Ecommerce.Order.Tests/DeliveryTests.cs
- [X] T003 [US1] `TryConfirmDeliveryAsync`, `ConfirmDeliveryCommand`, route; `ShipmentResponse` + id/deliveredAt/by
- [X] T004 [US2] Tests: shipped-not-delivered is on the way, delivered is due, payout claims delivered only; PayoutTests' helper delivers
- [X] T005 [US2] PayoutRepository requires DeliveredAt; `SaleDetailResponse.DeliveredAt`
- [X] T006 [US3] Tests: sweep delivers only parcels shipped before the cutoff, as "Auto"; twice → once; options validation
- [X] T007 [US3] `AutoConfirmDeliveriesCommand`, `DeliveryOptions`, `DeliveryConfirmationSweeper`, appsettings
- [X] T008 [US1] [US2] Client: receive button per parcel and on a one-parcel order; received badges; order delivered; seller sees received; earnings; vi/en; Vitest
- [X] T009 [P] [US1] Bruno (received 200, repeat 200, someone else's 404 - "not shipped" 409 is DeliveryTests', as one collection run ships only one order); verify-saga confirms the shipped parcel
- [X] T010 Run everything; mutation checks; CLAUDE.md

> Tests first this time on the server: the delivery commands, routes and responses were stubbed, and
> seven of the twelve `DeliveryTests` were seen RED (the five green were the shipped-time write and the
> startup validation, written just before, and a "not found" the stub answered vacuously). Eight guards
> were then removed one at a time - the due and claim filters, owner, shipped, repeat, the sweep window,
> the sweep overwriting a customer, the shipped time - and each turned a test red. `PayoutTests`' helper
> now delivers as well as ships: six of its tests went red on the semantic change, which was the point.
> The client code preceded its tests; six mutations each turned one red. End to end: verify-saga.sh
> confirms the parcel it ships; Bruno 121/121; Lan confirmed a parcel in the browser.

## Added in the 2026-09-27 backfill (work the pull request shows)

- [X] T011 [US1] `DeliveryConfirmOutcome` and `ParcelDelivery` (who, and the refusal wordings) in server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/Delivery.cs
- [X] T012 [US3] Index `IX_order_shipments_Status_DeliveredAt_ShippedAt` for the sweep's question, in OrderShipmentConfiguration.cs and the migration
- [X] T013 [US3] `EnsureShipmentsAsync` writes `ShippedAt` for parts created on demand for an older image's shipped order
- [X] T014 [US3] `DeliveryOptions.Register` (bind, validate both values at least 1, `ValidateOnStart`) and the hosted service registered in server/src/Services/Order/Ecommerce.Order.Infrastructure/DependencyInjection.cs
- [X] T015 [P] [US1] `components/order/receive-parcel` (confirm first: it releases the seller's payment) and `utils/order/delivery.ts` with its test
- [X] T016 [P] [US1] Bruno `admin-audit/staff read any order.yml` sets `shipmentId` from the order's first parcel, for the received requests that follow
- [X] T017 The design record completed to the specs/001 standard: data-model.md, quickstart.md, plan structure, research labels (2026-09-27)
- [X] T018 Merged as **#85** (`fe21aca`) on 2026-09-23 UTC: Order 164, client 166 tests; Bruno 121/121 requests, 192/192 tests; `verify-saga.sh` confirming the parcel it ships; server mutations 8/8, client 6/6
