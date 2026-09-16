# Message Contracts: Payment Service

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

Everything below already exists in `Ecommerce.Contracts.Payment` and is **not** being redesigned.
No new contract is added by this feature.

---

## Consumed

### `ProcessPaymentCommand`

```csharp
record ProcessPaymentCommand(Guid OrderId, Guid UserId, decimal Amount);
```

Published by `OrderStateMachine` on entering `InventoryReservedState`.

**Behaviour**: decide the outcome, record it, and reply with exactly one of the two events below
(FR-001). The amount is trusted as sent — see [research D4](../research.md).

**Rejection reasons** (FR-004):

| Condition | Reason text |
| :--- | :--- |
| Amount is zero or negative | `Invalid amount {amount} for order {id}` |
| The service is configured to reject | `Payment declined by the stub gateway (PAYMENT_OUTCOME=Reject)` |

The second reason names the setting deliberately: whoever reads it in a log at 2am should learn that
this is a configured stand-in refusing, not a provider declining a card.

---

## Published

### `PaymentProcessedEvent`

```csharp
record PaymentProcessedEvent(Guid OrderId, Guid PaymentId, DateTime ProcessedAt);
```

Correlated by `OrderId`. Moves the saga to publish `OrderCompletedEvent` and finalize; Inventory
consumes that as its confirmation signal and deducts the held units permanently.

`PaymentId` is the `payments.Id` of the row just written, so a saga trace can be followed to an
actual record.

### `PaymentFailedEvent`

```csharp
record PaymentFailedEvent(Guid OrderId, string Reason);
```

Correlated by `OrderId`. The saga publishes `ReleaseInventoryCommand` and finalizes, so the held
stock returns to available.

> This is the branch that has never run. Until this feature, no rejection could be produced except
> by publishing the event by hand.

Both replies go through the transactional outbox in the same transaction as the payment row, so a
reply can never exist without its record, or the reverse.

---

## Delivery guarantees the consumer must survive

| Property | Where it is handled |
| :--- | :--- |
| Same message delivered twice | MassTransit EF inbox, plus the unique `OrderId` constraint |
| Two deliveries at once | Unique violation caught, winning row re-read, reply reports that outcome |
| A reply lost in transit | The order waits and its stock expires; the payment record then disagrees with the order, which the payment lookup makes visible |
| Consumer crash mid-transaction | Transaction rolls back; the broker redelivers |
