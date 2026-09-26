# Contracts: Confirming a parcel arrived

> Completed on 2026-09-27, after the feature merged (#85), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. HTTP and configuration only: no message, no proto.

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

## As built (from the code at #85)

`POST /api/orders/{id:guid}/shipments/{shipmentId:guid}/received` - `OrdersController.ConfirmDelivery`,
under the controller's `[Authorize]` (any signed-in caller; the owner is the token's subject), through
the gateway's `/api/orders/{**catch-all}` route. No body.

| Situation | Answer |
| :-- | :-- |
| their shipped parcel, not yet delivered | `200` `OrderDetailResponse`; the parcel now has `deliveredAt` and `deliveryConfirmedBy: "Customer"` |
| already delivered (by them or by the sweep) | `200`, nothing changed - the first word stands |
| a parcel of theirs not shipped yet | `409` `This parcel has not been shipped yet.` |
| no such order or parcel, a parcel of a different order, or not theirs | `404` `Order not found.` |
| no token | `401` |

Response fields added (all nullable, so an older client ignores them):

| Record | Field | Meaning |
| :-- | :-- | :-- |
| `ShipmentResponse` | `id` | the part's id - what the route names |
| `ShipmentResponse` | `deliveredAt` | when delivered; null until then |
| `ShipmentResponse` | `deliveryConfirmedBy` | `Customer` or `Auto` |
| `SaleDetailResponse` | `deliveredAt` | the seller's own part's delivery |

`GET /api/orders/sales/balance` and `GET /api/orders/payouts/due`, and the claim behind
`POST /api/orders/payouts`, count a part as due only once `deliveredAt` is set.

Bruno: `admin-audit/the customer says the parcel arrived` (200), `admin-audit/saying it arrived again
changes nothing` (200), `seller/saying someone else s parcel arrived is 404`. The 409 for an unshipped
parcel is covered by `DeliveryTests`, not Bruno - one collection run ships only one order.

## Configuration, as validated

| Key | Default | Rule |
| :-- | :-- | :-- |
| `Delivery:AutoConfirmDays` | `7` | at least 1, validated on start (`ValidateOnStart`) - Order does not start otherwise |
| `Delivery:SweepIntervalMinutes` | `60` | at least 1, validated on start |

Both are in Order's `appsettings.json`; environment variables override them in the usual way
(`Delivery__AutoConfirmDays`).
