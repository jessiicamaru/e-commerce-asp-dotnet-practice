# Contracts: Confirming a parcel arrived

## New

`POST /api/orders/{orderId}/shipments/{shipmentId}/received` (signed in; own order) → 200 order detail.
- 404 `Order not found.` - no such order or parcel, or not theirs.
- 409 `This parcel has not been shipped yet.`
- A repeat is 200 and changes nothing.

## Changed (additive)

- Order detail `shipments[]` gain `id`, `deliveredAt`, `deliveryConfirmedBy`.
- A seller's sale detail gains `deliveredAt`.
- Balances and payouts: a shipped parcel is **on the way** until delivered, then **due**.

## Configuration

```json
"Delivery": { "AutoConfirmDays": 7, "SweepIntervalMinutes": 60 }
```
