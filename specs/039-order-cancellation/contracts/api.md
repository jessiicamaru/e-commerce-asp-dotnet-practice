# Contracts: Cancelling a paid order

> Completed on 2026-09-27, after the feature merged (#84), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. The event is described in full in [messages.md](messages.md).

## Message - new

```csharp
// Ecommerce.Contracts/Order
public record OrderCancelledEvent(Guid OrderId, DateTime CancelledAt, string CancelledBy); // "Customer" | "Staff"
```

Published by Order (outbox); consumed by Inventory (`RestockCancelledOrderConsumer`) and Payment
(`RefundCancelledOrderConsumer`).

## HTTP - new

- `POST /api/orders/{id}/cancel` (signed in; own order only) → 200 order detail, `status: "Cancelled"`.
  - 404 `Order not found.` - none, or not theirs.
  - 409 `This order is being prepared; ask the shop to cancel it.`
  - 409 `Part of this order has been shipped; it can no longer be cancelled.`
  - 409 `Only a paid order can be cancelled.` - still settling, or failed.
  - A second cancel returns 200 and changes nothing.
- `POST /api/orders/fulfilment/{id}/cancel` (Admin) - the same, without the "being prepared" refusal.

## HTTP - changed

- Order detail gains `cancelledBy` (`"Customer"`, `"Staff"` or null).
- A seller's sales include cancelled ones, `status: "Cancelled"`, with no `shippingAddress`.
- `POST /api/orders/sales/{id}/preparing|shipment` on a cancelled sale of theirs → 409 `This order was cancelled.`
- `GET /api/payments/{orderId}` (Admin) gains `refundedAmount` and `refundedAt` (null when none).

## As built (from the code at #84)

| Route | Auth | Refusals |
| :-- | :-- | :-- |
| `POST /api/orders/{id}/cancel` | any signed-in caller; the owner is the token's subject | `401` no token; `404` none or not theirs; `409` the three wordings above |
| `POST /api/orders/fulfilment/{id}/cancel` | `[Authorize(Roles = "Admin")]` | `401`; `403` for anyone not Admin; `404` none; `409` shipped or not paid - never "being prepared" |

Neither takes a body. Both answer `200` with `OrderDetailResponse` (the customer's read through the
owner-scoped lookup, the staff's through the staff one), including when the order was already cancelled -
a repeat publishes nothing.

A seller moving a cancelled order's part: `409 This order was cancelled.` only if they have a part on it
(`ShipmentMoveOutcome.OrderCancelled`); otherwise the usual `404 Sale not found.` A cancelled sale reads
`status: "Cancelled"` whatever state its part was left in, with no `shippingAddress`.

`GET /api/payments/{orderId}` and the payments list: `PaymentResponse` gains `refundedAmount` and
`refundedAt`, both null when there is no refund.

Bruno: `admin-audit/cancelling a shipped order is 409 for its customer`, `... for staff`,
`security-checks/cancelling an order without a token is 401`, `seller/cancelling someone else s order is 404`.
