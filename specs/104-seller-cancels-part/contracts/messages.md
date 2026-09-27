# Message contract: A seller cancels the part of an order they cannot fulfil

## `Ecommerce.Contracts.Order.OrderPartCancelledEvent` (new)

```csharp
public record OrderPartCancelledEvent(
    Guid OrderId,
    Guid PartId,            // order_shipments.Id
    List<Guid> VariantIds,  // the part's lines
    decimal Amount,         // what the part refunds: goods less discounts, plus tax (research D2)
    string Currency,
    DateTime CancelledAt,
    string CancelledBy);    // "Seller" or "Staff"
```

- **Publisher**: Order, staged inside the cancel's transaction before its one save. It is published only when other
  parts remain. The last part publishes `OrderCancelledEvent` instead (specs/039).
- **Consumers**:
  - **Inventory `RestockCancelledPartConsumer`** returns the reservations of `VariantIds` for `OrderId` (research D3).
    It is idempotent by reservation status.
  - **Payment `RefundCancelledPartConsumer`** records a refund of `Amount`. It is idempotent through the unique
    `refunds.PartId`.
- The consumer names are unique across services (the queue-name gotcha in CLAUDE.md).
- **Deploy the consumers first** (research D8).

## `OrderCancelledEvent`: meaning widened

`CancelledBy` may now also be `"Seller"`, when a seller cancels the last part. Payment's whole-order refund becomes the
amount charged less the order's part refunds.

## `UserNotificationRequested`: new kind

`PartCancelled`, sent to the buyer. Data `{ "orderId", "reason", "shop"? }` (no `shop` for the shop's own part).
Link `/orders/{orderId}`.
