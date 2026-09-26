# Message Contracts: Saga payment timeout

> Written on 2026-09-27, after the feature merged (#135), from the code at that merge, the pull request and
> docs/architecture/saga-orchestration-roadmap.md.

**Feature**: [spec.md](../spec.md)

One contract added to `Ecommerce.Contracts` (21 messages at the merge, per the regenerated
[docs/reference/messages.md](../../../docs/reference/messages.md)), one message internal to the orchestrator,
and new publications of existing contracts. No HTTP endpoint and no gRPC contract changed.

## Added: `RefundPaymentCommand` - `Ecommerce.Contracts.Payment`

```csharp
public record RefundPaymentCommand(Guid OrderId, string Reason);
```

**Publisher**: the Orchestrator's `OrderStateMachine`, in `PaymentTimedOut` on a late `PaymentProcessedEvent`,
through the orchestrator's transactional outbox. Reason: "The payment was approved after the order had failed
waiting for it."

**Consumer**: Payment's `RefundLatePaymentConsumer` (queue named after the class) →
`RefundOrderCommand(OrderId, Reason)`.

**Carries no amount on purpose**, like `OrderCancelledEvent`: Payment refunds what its own `payments` row says it
took.

**Idempotency**: `refunds.OrderId` is unique (specs/039). The handler returns false and records nothing when
there is no approved payment or the refund already exists; a redelivery is a no-op.

**Deployment**: additive, but the orchestrator and Payment must be deployed together, or the command has no
consumer. Publish rebuilds every image anyway.

## Internal: `PaymentTimeoutExpired` - orchestrator only

```csharp
// server/src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/Timeouts/PaymentTimeoutExpired.cs
public record PaymentTimeoutExpired(Guid OrderId);
```

Not in `Ecommerce.Contracts`: no other service publishes or consumes it. Published by `PaymentTimeoutSweeper`
through the outbox, consumed by the saga, correlated by `OrderId`.

| Saga state on arrival | Effect |
| :--- | :--- |
| `InventoryReservedState` | Publishes `ReleaseInventoryCommand` and `OrderFailedEvent`; → `PaymentTimedOut` |
| `PaymentTimedOut` | Ignored (a second sweeper or tick) |
| no instance | Discarded, no log (the order already finished) |

## Existing contracts, new publications

| Message | Newly published when | Consumers (unchanged) |
| :--- | :--- | :--- |
| `ReleaseInventoryCommand(OrderId, Reason)` | On timeout, reason "Payment did not answer in time" | Inventory releases the `Held` reservation |
| `OrderFailedEvent(OrderId, Reason, FailedAt)` | On timeout, reason "Payment did not answer in time." | Order settles `Failed` (guarded `WHERE Status = 'Submitted'`) and tells the buyer; Cart marks the checkout failed and removes nothing |

## Existing contracts, new handling

| Message | In `PaymentTimedOut` |
| :--- | :--- |
| `PaymentProcessedEvent` | Records `PaymentId`, publishes `RefundPaymentCommand`, finalises. No `OrderCompletedEvent` |
| `PaymentFailedEvent` | Finalises; nothing to refund |

## Payment's internal command

`RefundOrderCommand(Guid OrderId, string Reason = RefundOrderCommand.Cancelled)` - the reason ("the order was
cancelled" by default, so specs/039's `RefundCancelledOrderConsumer` is unchanged) appears in the
`RefundRecorded` audit entry's summary and snapshot, and in the log line.
