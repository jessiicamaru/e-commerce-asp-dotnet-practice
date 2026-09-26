# Message Contracts: In-app notifications

> Written on 2026-09-27, after the feature merged (#94), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

One new integration message, published by Order and consumed by Activity. It is the only new contract; no
existing message changed.

---

## `UserNotificationRequested` - `Ecommerce.Contracts.Activity`

`server/src/BuildingBlocks/Ecommerce.Contracts/Activity/UserNotificationRequested.cs`

```csharp
public record UserNotificationRequested(
    Guid NotificationId,     // minted by the publisher; the notification's key
    Guid RecipientId,
    string Kind,             // e.g. "OrderPaid"; the storefront words it
    Dictionary<string, string> Data,   // what the words need, as strings - never a sentence
    string? Link,            // where it points in the storefront
    DateTime OccurredAt);
```

**Publisher**: any service through `Ecommerce.Shared.Notifications.INotifier` (`Notifier`, registered with
`AddNotifier()`), which publishes through `IPublishEndpoint` - that is, **through the calling service's
transactional outbox**, in the transaction of the change it announces (research D2). At this merge only Order
registers it. `Notifier` mints `NotificationId` with `Guid.CreateVersion7()` and sets `OccurredAt` to
`DateTime.UtcNow`.

**Consumer**: Activity's `RecordNotificationConsumer` (queue prefixed `ActivitySvc`, so it never shares a
queue with a same-named consumer elsewhere), which sends `RecordNotificationCommand` and inserts with
`ON CONFLICT ("Id") DO NOTHING`.

**Idempotency**: two layers, as for the audit log - MassTransit's EF inbox on Activity's endpoints, and the
primary key on the publisher's id. A redelivery is zero rows (research D5).

**Ordering**: none needed. Each message is one row; the inbox sorts by `OccurredAt`, not by arrival.

---

## The kinds Order sends at this merge

From `NotificationKind` in `Ecommerce.Shared/Notifications/Notifier.cs` and `OrderNotices`:

| Kind | Recipient | `Data` | `Link` | Sent from (inside its transaction) |
| :-- | :-- | :-- | :-- | :-- |
| `OrderPaid` | buyer | `orderId`, `total`, `currency` | `/orders/{id}` | `CompleteOrderCommandHandler` - the `stage` of `TrySettleAsync(Paid)` |
| `NewSale` | each distinct seller on the order | `orderId` | `/shop/sales/{id}` | The same `stage` |
| `OrderFailed` | buyer | `orderId` | `/orders/{id}` | `FailOrderCommandHandler` - the `stage` of `TrySettleAsync(Failed)` |
| `ParcelShipped` | buyer | `orderId`, `tracking`, `shop` (a seller's parcel only) | `/orders/{id}` | `ParcelAudit.RecordMoveAsync`, from `TryMoveShipmentAsync`'s `stage` - staff (`ShipOrderCommandHandler`) or a seller (`ShipMySaleCommandHandler`) |
| `OrderCancelled` | buyer | `orderId`, `by` (`Customer` or `Staff`) | `/orders/{id}` | `CancelStep.RunAsync`'s `stage` - customer or staff |
| `SaleCancelled` | each distinct seller on the order | `orderId` | `/shop/sales/{id}` | The same `stage` |
| `ParcelReceived` | the parcel's seller; nobody for the shop's own | `orderId` | `/shop/sales/{id}` | `ConfirmDeliveryCommandHandler` - `TryConfirmDeliveryAsync`'s `stage` |
| `PayoutRecorded` | the paid seller | `amount`, `currency` | `/shop/payouts` | `RecordPayoutCommandHandler` - `TryRecordAsync`'s `stage` |

Amounts are formatted with the invariant culture and pattern `0.##` (`"22462000"`, `"12.5"`). `tracking` is
the trimmed reference the shipper entered. `shop` is the seller's shop name frozen on the order line
(specs/036), absent for the shop's own parcel, which the storefront words as "the shop".

Not sent at this merge: anything for a parcel moved to `Preparing`; `ParcelReceived` for a parcel the delivery
sweep took as delivered (`AutoConfirmDeliveriesCommand` - later `ParcelAutoDelivered`, specs/059).

---

## Existing messages whose handling changed

No contract changed, but two consumers in Order now notify, which is where the transaction bug appeared:

| Message | Consumer (Order) | New effect | Note |
| :-- | :-- | :-- | :-- |
| `OrderCompletedEvent` | `OrderCompletedConsumer` → `CompleteOrderCommand` | `OrderPaid` + `NewSale` notices and an `OrderPaid` audit entry, staged with the settle | The consumer outbox already holds a transaction; `TrySettleAsync` joins it (research D6) |
| `OrderFailedEvent` | `OrderFailedConsumer` → `FailOrderCommand` | `OrderFailed` notice and an `OrderFailed` audit entry | Same |

`AuditEntryRecorded` (specs/041) carries the two new audit actions; its shape is unchanged.

---

## Delivery guarantees the consumer must survive

| Property | Where it is handled |
| :-- | :-- |
| Same message delivered twice, or four times at once | Inbox, plus `ON CONFLICT ("Id") DO NOTHING` on the publisher's id |
| The publisher's change rolled back | The outbox row rolled back with it; nothing is ever published |
| The saga's outcome delivered twice | The second guarded settle affects zero rows; its `stage`, which holds the notices, is not called |
| Activity down | Messages wait in Order's outbox and the broker; stored when it returns |
| A kind Activity has never seen | Stored as it is; Activity does not validate kinds |
