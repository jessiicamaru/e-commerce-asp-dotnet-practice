# Contracts: Cancelling a paid order

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
