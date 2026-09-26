# Feature Specification: Saga payment timeout

> Completed on 2026-09-27, after the feature merged (#135), from the code at that merge, the pull request and
> docs/architecture/saga-orchestration-roadmap.md.

**Feature Branch**: `053-saga-payment-timeout` | **Created**: 2026-09-24 | **Issue**: #123

**Status**: Merged (#135, 2026-09-24)

**Input**: Issue #123 - once stock is reserved the checkout saga waits for Payment without limit, while
Inventory's hold on that stock expires after 15 minutes.

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

## User Scenarios & Testing *(mandatory)*

### US1 - An unanswered payment fails the order in time (Priority: P1)

An order has reserved stock and waited `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS` (default 600) with no
answer from Payment. The saga then:
- releases the stock (`ReleaseInventoryCommand`);
- fails the order (`OrderFailedEvent`, with a reason);
- waits in `PaymentTimedOut` for a late answer.

The customer's cart is untouched, because a failed order never removes lines, and they are told the
order failed (specs/042).

**Why this priority**: Without it an order can sit `Submitted` for ever, or be paid for after its stock went
back on the shelf - both money and stock wrong, and nothing reports it.

**Independent Test**: Stop Payment, place an order with a short timeout, and watch the order fail and the stock
come back with nothing held.

**Acceptance Scenarios**:

1. With Payment stopped, an order fails once the timeout has passed, and the stock is back on the shelf
   in full, with nothing held.
2. The timeout is shorter than the hold. The orchestrator refuses to start when both are configured
   and the timeout is not shorter.

---

### US2 - A payment that answers late is refunded (Priority: P1)

When Payment answers after the timeout:
- an approval is refunded: the saga sends `RefundPaymentCommand`, and Payment records one refund of
  what it charged, through the once-only refund path of specs/039;
- a rejection needs nothing.

Either way the saga instance finishes, and the order stays `Failed`.

**Why this priority**: US1 alone would fail the order and then keep the money of a late approval.

**Independent Test**: After US1's order has failed, restart Payment; it approves the queued charge and one full
refund is recorded, with the order still `Failed`.

**Acceptance Scenarios**:

1. Payment is restarted after the timeout. It approves the queued charge, and exactly one refund of the
   full amount is recorded. The order is still `Failed`, and no stock moves.
2. A payment that answers **before** the timeout completes the order exactly as today. A timeout that
   arrives after that changes nothing.

---

### US3 - The saga is tested at all (Priority: P2)

Before this feature, the saga's transitions were exercised only by `verify-saga.sh` end to end. A new
test project covers the state machine through MassTransit's test harness: the happy path, the
compensation, the timeout and both late answers. It also covers the sweeper, against real PostgreSQL.

**Why this priority**: The new transitions need a test that can fail; the existing ones had none below the
end-to-end script.

**Independent Test**: `dotnet test tests/Ecommerce.Orchestrator.Tests`.

**Acceptance Scenarios**:

1. **Given** the harness, **When** each path is driven by messages, **Then** the saga publishes what it should
   and ends in the right state.
2. **Given** saga rows in PostgreSQL, **When** the sweeper's query runs, **Then** it returns only orders still
   waiting for Payment past the cutoff.

### Edge Cases

- **A payment approved just before the timeout.** The order completes and the instance is finalised; the
  timeout then finds no instance and is discarded.
- **Two sweepers, or two ticks, announce the same order.** The second timeout arrives in `PaymentTimedOut` and
  is ignored - not faulted.
- **The orchestrator restarts while orders wait.** Nothing is lost: the timer is the saga table, read again at
  the next tick.
- **A backlog.** At most 200 orders are announced per tick.
- **The hold is not visible to the orchestrator** (run on its own without `INVENTORY_RESERVATION_TTL_MINUTES`).
  The cross-check is skipped; the timeout and sweep are still validated.
- **An older orchestrator after a rollback.** It cannot load an instance in `PaymentTimedOut`; a late answer for
  such an order faults into the error queue rather than being lost.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The timer survives a restart. It is read from the saga table by a sweeper, not held in
  memory. RabbitMQ in compose and CI has no delayed-message plugin, so MassTransit's `Schedule` is not
  available.
- **FR-002**: Safe on several orchestrator instances. A duplicate timeout for an order already timed out
  or finished is ignored.
- **FR-003**: `RefundPaymentCommand` is a contract in `Contracts/Payment`, published by the orchestrator
  and consumed by Payment. `RefundOrderCommand` gains a reason, so the audit entry says why.
- **FR-004**: Everything the saga publishes goes through its outbox (Principle III). That includes what
  the sweeper publishes.
- **FR-005**: `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS` (default 600) and `ORCHESTRATOR_TIMEOUT_SWEEP_SECONDS`
  (default 30) must be positive whole numbers; when `INVENTORY_RESERVATION_TTL_MINUTES` is visible, the timeout
  plus one sweep must be shorter than it. Otherwise the orchestrator refuses to start, naming each problem.

### Key Entities

- **Saga instance** (`order_state_data`): gains one reachable state value, `PaymentTimedOut`. `UpdatedAt`, set on
  entering `InventoryReservedState`, is when the wait began.
- **Refund** (`refunds`, Payment, specs/039): at most one per order; now also recorded for a late approval, with
  the reason in its audit entry.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With Payment stopped, no order stays `Submitted` longer than the timeout plus one sweep interval
  (plus message delivery); its stock is released with nothing held.
- **SC-002**: A payment approved after the timeout produces exactly one refund of the full amount; the order
  stays `Failed` and no stock moves.
- **SC-003**: The orchestrator does not start with a timeout plus sweep that is not shorter than the hold.
- **SC-004**: The saga has a test project; each new transition has a test that failed before it existed.

## Assumptions

- Inventory's hold and the saga's wait start at the same moment, give or take one message hop.
- Payment refunds what its own payment row says it took; the command carries no amount (as specs/039's
  cancellation does not).

## Out of scope

- A timeout on the reservation step. Inventory answers from its own database, and a reservation that
  fails already fails the order.
- Showing the customer "refunded" on a failed order. A timed-out order reads as failed; a refund of a
  failed order is recorded, not displayed.
