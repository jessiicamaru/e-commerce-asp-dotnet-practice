# Message Contracts: A Total With Something Behind It

> Written on 2026-09-27, after the feature merged (#32), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](../spec.md) | **Decision**: [research.md D5](../research.md)

**No message changed.** Nothing in `Ecommerce.Contracts` was edited, and no publisher or consumer was
added.

## The one message this feature depends on

### `OrderSubmittedEvent` - `Ecommerce.Contracts.Order`

Published by Order's `SubmitOrderCommandHandler` through the transactional outbox, in the same
`SaveChangesAsync` as the order row. Consumed by the Orchestrator's saga (and by Cart, feature 010).

What changed is the **meaning of the number, not the field**: `TotalAmount` is now the grand total -
subtotal + delivery + tax − discount - where before it was goods plus delivery. The saga copies it into
`ProcessPaymentCommand`, and Payment charges it. That is how FR-008 holds with no contract change: the
amount Payment is asked for is, by construction, the `TotalAmount` stored on the row.

Evidence from the pull request: `verify-saga.sh` against the containers recomputed
29.97 + 15.00 + 3.00 + 1.50 = 49.47 from the stored rate, and Payment was asked for 49.47.

## Idempotency

Unchanged. The feature adds no consumer, so it raises no new redelivery question; settlement still
happens through Order's guarded `UPDATE ... WHERE "Status" = 'Submitted'`, which does not touch the
new columns.
