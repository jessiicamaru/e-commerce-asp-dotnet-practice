# Message Contracts: Returning a delivered parcel

> Written on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

One new integration event, and five new notice kinds carried by the existing `UserNotificationRequested`.

---

## `ParcelReturnedEvent` - `Ecommerce.Contracts.Order` (new)

```csharp
public record ParcelReturnedEvent(
    Guid ReturnId,
    Guid OrderId,
    Guid ShipmentId,
    List<ReturnedItemDto> Items,
    decimal Amount,
    string Currency,
    DateTime ReturnedAt);

public record ReturnedItemDto(Guid VariantId, int Quantity);
```

**Publisher**: Order, inside the transaction whose guarded `UPDATE` moved the return `SentBack` → `Received`, through
the EF outbox - so the event exists if and only if the return is received.

**Fields**: `Items` are the parcel's order lines (`order_items` of that seller), each the variant id - or, for a line
from before variants, the product id, which the product's first variant reuses (specs/020). `Amount` is goods plus
their tax as frozen at checkout, never the delivery. `Currency` is the order's.

Unlike `OrderCancelledEvent`, which carries nothing, this carries its lines and its amount: a return is part of an
order, and only Order knows which lines were that part and what was paid for them.

### Consumers

| Service | Consumer | Command | Idempotency |
| :-- | :-- | :-- | :-- |
| Payment | `RefundReturnedParcelConsumer` | `RefundReturnCommand(ReturnId, OrderId, Amount, Currency)` | Unique `refunds.ReturnId`: a redelivery finds the refund and stops; two at once both insert and one gets 23505, answered as "already refunded" after `DiscardPendingChanges()` |
| Inventory | `RestockReturnedParcelConsumer` | `RestockReturnedParcelCommand(ReturnId, OrderId, Items)` | `returned_parcels` claimed with `ON CONFLICT ("ReturnId") DO NOTHING` in the transaction that moves the stock |

The two consumers are **named for what they do**: two classes with the same name would share one queue, and the event
would reach one service instead of both (CLAUDE.md gotcha).

**Payment's checks** (it refuses and logs an error rather than record): no approved payment for the order; a currency
other than the payment's; an amount of zero or less; refunds for the order that would exceed the amount paid.

**Inventory's behaviour**: locks the named variants' stock rows `FOR UPDATE` in ascending order, adds each quantity to
`QuantityOnHand`, logs a warning for a variant with no stock row (not invented), announces
`StockAvailabilityChangedEvent` through `StockAvailabilityAnnouncer` - the eighth path that moves stock - and records
an audit entry `StockReturned`.

---

## Published in turn

| Message | By | When |
| :-- | :-- | :-- |
| `StockAvailabilityChangedEvent` (existing) | Inventory | After a restock, for the variants it touched |
| `AuditEntryRecorded` (existing) | Order, Payment, Inventory | Every return step (`ReturnRequested`, `ReturnAccepted`, `ReturnRefused`, `ReturnEscalated`, `ReturnRejected`, `ReturnSentBack`, `ReturnReceived`), `RefundRecorded`, `StockReturned` |

---

## Notices - `UserNotificationRequested` (existing contract, five new kinds)

Declared in `Ecommerce.Shared/Notifications/notification-kinds.json` and worded in
`client/src/locales/{en,vi}/notifications.json`. Staged in the same transaction as the step that causes them.

| Kind | To | Data keys | Link |
| :-- | :-- | :-- | :-- |
| `ReturnRequested` | the parcel's seller | `orderId` | `/shop/sales/{orderId}` |
| `ReturnSentBack` | the parcel's seller | `orderId`, `tracking` | `/shop/sales/{orderId}` |
| `ReturnAccepted` | the buyer | `orderId` | `/orders/{orderId}` |
| `ReturnRefused` | the buyer | `orderId`, `reason` | `/orders/{orderId}` |
| `ReturnRefunded` | the buyer | `orderId`, `amount`, `currency` | `/orders/{orderId}` |

The shop's own parcel has no seller to tell, so `ReturnRequested` and `ReturnSentBack` are not sent for it.
