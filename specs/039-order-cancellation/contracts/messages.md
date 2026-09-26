# Message Contract: Cancelling a paid order

> Written on 2026-09-27, after the feature merged (#84), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

One new integration message. Nothing existing changed.

---

## `OrderCancelledEvent` — `Ecommerce.Contracts.Order`

```csharp
// server/src/BuildingBlocks/Ecommerce.Contracts/Order/OrderSubmittedEvent.cs (declared in this file)
public record OrderCancelledEvent(
    Guid OrderId,
    DateTime CancelledAt,
    string CancelledBy   // "Customer" | "Staff"
);
```

**Publisher**: Order, from `CancelStep.RunAsync` (`CancelMyOrderCommand`, `CancelOrderCommand`). Staged
through `IPublishEndpoint` by a callback **inside** `OrderRepository.TryCancelAsync`'s transaction, after
the guarded `UPDATE` and before the one `SaveChangesAsync` - so it reaches the EF outbox with the row
change and commits with it or not at all. A refused cancellation and a repeat publish nothing.

**Carries no items and no amount**, on purpose (research D1): each consumer undoes its own part from its
own rows.

**Not involved**: the Orchestrator. The saga ended at payment; this is choreography after it.

## Consumers

| Service | Consumer | Does | Idempotent by |
| :-- | :-- | :-- | :-- |
| Inventory | `RestockCancelledOrderConsumer` → `RestockCancelledOrderCommand` | `Confirmed` reservations: back on hand; `Held`: hold released; both → `Released`, reason `Returned: order cancelled`; announces availability | the guarded status each row moves **from**, under `FOR UPDATE` on the stock rows; a second delivery finds no `Confirmed` or `Held` row |
| Payment | `RefundCancelledOrderConsumer` → `RefundOrderCommand` | records a `refunds` row of the approved payment's amount, currency and provider | unique `refunds.OrderId`; a concurrent loser gets 23505, discards its pending changes and is answered "already refunded" |

⚠️ **The class names are the queue names.** Two classes called `OrderCancelledConsumer` would bind to one
queue and each event would reach only one of the two services (research D6). On the running stack each
queue had exactly one consumer.

## Delivery cases the consumers survive

| Case | Outcome |
| :-- | :-- |
| the same event twice | Inventory moves nothing the second time; Payment finds the refund and records none |
| two deliveries at once (Payment) | one insert wins, the other hits the unique index (`A_delivery_that_loses_the_race_is_told_it_was_already_refunded`) |
| cancellation before `OrderCompletedEvent` (Inventory) | the hold is released; the late completion finds no `Held` row and deducts nothing |
| an order Inventory never reserved for | no-op, logged |
| a rejected payment, or no payment | no refund, logged |
