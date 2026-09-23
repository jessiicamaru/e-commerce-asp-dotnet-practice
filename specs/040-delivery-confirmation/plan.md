# Implementation Plan: Confirming a parcel arrived

**Branch**: `040-delivery-confirmation` | **Spec**: [spec.md](spec.md) | [research](research.md) |
[contracts](contracts/api.md)

## Technical Context

- **Order only** (plus the client). `OrderShipment.ShippedAt/DeliveredAt/DeliveryConfirmedBy`, migration
  `AddShipmentDelivery` (backfill `ShippedAt`); `TryMoveShipmentAsync` writes `ShippedAt`.
- `IOrderRepository.TryConfirmDeliveryAsync(orderId, shipmentId, ownerId, at)` and
  `AutoConfirmDeliveriesAsync(cutoff, at)`; `ConfirmDeliveryCommand`, `AutoConfirmDeliveriesCommand`.
- `DeliveryOptions` (validated at startup) + `DeliveryConfirmationSweeper` (hosted service).
- `PayoutRepository`: due and the claim require `DeliveredAt IS NOT NULL`; on the way is everything else.
- Responses: `ShipmentResponse` + `Id`, `DeliveredAt`, `DeliveryConfirmedBy`; `SaleDetailResponse` + `DeliveredAt`.
- Client: "I've received it" per shipped parcel (and on a one-parcel order), received badges, the order
  reads delivered when every parcel is; seller sees received; earnings due only once delivered.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Order's own rows; no event - nothing else needs to know. |
| II - Clean Architecture | Commands in Application; the sweeper and SQL in Infrastructure. |
| III - Atomic, idempotent | Every confirmation is one guarded UPDATE; repeats and concurrent sweeps affect nothing. |
| IV - Identity from the token | Owner from the token, in the same query. |
| V - Evidence | Tests: confirm, refusals, repeat, owner, sweep window, sweep idempotence, payouts only delivered; verify-saga confirms. |
| Schema compatibility | Three nullable columns; no enum value (research D1). |
