# Feature Specification: Saga payment timeout

**Feature Branch**: `053-saga-payment-timeout` | **Created**: 2026-09-24 | **Issue**: #123

## Why

The checkout saga reserves stock, asks Payment, and then waits, with no limit. Inventory does not wait
without limit: its sweeper returns a hold to the shelf after `INVENTORY_RESERVATION_TTL_MINUTES` (15).

**A payment answered after that.** The saga completes the order and Order settles it `Paid`. Inventory's
`ConfirmStockCommandHandler` finds no `Held` reservation and deducts nothing, as
`SettlementTests.A_settlement_arriving_after_expiry_changes_nothing` confirms. So the shop has been paid
for stock it put back on the shelf, where somebody else can buy it.

**A Payment that never answers.** The order stays `Submitted` forever, and the customer sees it settling
indefinitely.

The stub answers at once, so neither happens today. A real provider, or a Payment that is down, makes
both ordinary.

## User Scenarios

### US1 - An unanswered payment fails the order in time (P1)

An order has reserved stock and waited `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS` (default 600) with no
answer from Payment. The saga then:
- releases the stock (`ReleaseInventoryCommand`);
- fails the order (`OrderFailedEvent`, with a reason);
- waits in `PaymentTimedOut` for a late answer.

The customer's cart is untouched, because a failed order never removes lines, and they are told the
order failed (specs/042).

**Acceptance**
1. With Payment stopped, an order fails once the timeout has passed, and the stock is back on the shelf
   in full, with nothing held.
2. The timeout is shorter than the hold. The orchestrator refuses to start when both are configured
   and the timeout is not shorter.

### US2 - A payment that answers late is refunded (P1)

When Payment answers after the timeout:
- an approval is refunded: the saga sends `RefundPaymentCommand`, and Payment records one refund of
  what it charged, through the once-only refund path of specs/039;
- a rejection needs nothing.

Either way the saga instance finishes, and the order stays `Failed`.

**Acceptance**
1. Payment is restarted after the timeout. It approves the queued charge, and exactly one refund of the
   full amount is recorded. The order is still `Failed`, and no stock moves.
2. A payment that answers **before** the timeout completes the order exactly as today. A timeout that
   arrives after that changes nothing.

### US3 - The saga is tested at all (P2)

Before this feature, the saga's transitions were exercised only by `verify-saga.sh` end to end. A new
test project covers the state machine through MassTransit's test harness: the happy path, the
compensation, the timeout and both late answers. It also covers the sweeper, against real PostgreSQL.

## Requirements

- **FR-001**: The timer survives a restart. It is read from the saga table by a sweeper, not held in
  memory. RabbitMQ in compose and CI has no delayed-message plugin, so MassTransit's `Schedule` is not
  available.
- **FR-002**: Safe on several orchestrator instances. A duplicate timeout for an order already timed out
  or finished is ignored.
- **FR-003**: `RefundPaymentCommand` is a contract in `Contracts/Payment`, published by the orchestrator
  and consumed by Payment. `RefundOrderCommand` gains a reason, so the audit entry says why.
- **FR-004**: Everything the saga publishes goes through its outbox (Principle III). That includes what
  the sweeper publishes.

## Out of scope

- A timeout on the reservation step. Inventory answers from its own database, and a reservation that
  fails already fails the order.
- Showing the customer "refunded" on a failed order. A timed-out order reads as failed; a refund of a
  failed order is recorded, not displayed.
