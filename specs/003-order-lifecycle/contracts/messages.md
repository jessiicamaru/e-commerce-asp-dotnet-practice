# Message Contracts: Order Lifecycle Visibility

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-16

**This feature adds no message contract.** Both announcements it consumes already exist in
`Ecommerce.Contracts/Order/OrderSubmittedEvent.cs` and are already published by the orchestrator.
Nothing new is published.

That is the most important line in this document: a change to `Ecommerce.Contracts` is breaking even
when it compiles, because every other service deserializes it. This feature does not touch it.

---

## Consumed

### `OrderCompletedEvent`

```csharp
public record OrderCompletedEvent(
    Guid OrderId,
    DateTime CompletedAt
);
```

**Published by**: `OrderStateMachine`, during `InventoryReservedState`, on `PaymentProcessed`, at the
same moment it finalizes.

**Already consumed by**: `Ecommerce.Inventory` (`OrderCompletedConsumer`), which uses it as its
confirmation signal. Order becomes a **second, independent** subscriber — MassTransit fans a
published message out to every subscribing service's queue, so adding this consumer does not take
the message away from Inventory. Worth stating because "the event is already consumed" is a natural
reason to think it cannot be consumed again.

**Effect in Order**: `Submitted` → `Completed`, `UpdatedAt = CompletedAt`.

**Note on `CompletedAt`**: it is the orchestrator's clock, not this service's. It is recorded as
sent rather than replaced with `DateTime.UtcNow`, so the two services agree on when the order
finished.

---

### `OrderFailedEvent`

```csharp
public record OrderFailedEvent(
    Guid OrderId,
    string Reason,
    DateTime FailedAt
);
```

**Published by**: `OrderStateMachine`, on **both** failure branches — read from the state machine,
not inferred:

| Branch | State | Reason text originates from |
| :--- | :--- | :--- |
| Stock could not be reserved | `During(Submitted)` on `InventoryReservationFailed` | `InventoryReservationFailedEvent.Reason` |
| Payment was rejected | `During(InventoryReservedState)` on `PaymentFailed` | `PaymentFailedEvent.Reason` |

The issue describes only the payment path. The reservation path exists too, and FR-003 covers both
because of this reading.

**Already consumed by**: nobody. This is the first consumer.

**Effect in Order**: `Submitted` → `Failed`, `FailureReason = Reason`, `UpdatedAt = FailedAt`.

**Length**: `FailureReason` is `varchar(512)`. `Reason` on the contract is unbounded. The consumer
truncates rather than letting the insert fail — a settlement that fails because the explanation was
long is worse than a settlement with a clipped explanation.

---

## Published

None. Both consumers write one row and return.

This is why constitution III's publish-ordering clause has nothing to bite on here: there is no
event whose atomicity with the write could be broken. Only its idempotency clause applies.

---

## Delivery guarantees relied on

| Guarantee | Provided by | Notes |
| :--- | :--- | :--- |
| At-least-once delivery | RabbitMQ + MassTransit | Redelivery is normal operation, not an error |
| Duplicate suppression | `InboxState`, via `UseEntityFrameworkOutbox<OrderDbContext>` on every receive endpoint | Tables already exist in the initial migration — see research D3 |
| Correct behaviour when duplicate suppression is bypassed | The guarded `UPDATE` | **This is the actual guarantee.** The tests must pass with the inbox removed |

The ordering of the last two rows is deliberate and is the point of the table.
