# Feature Specification: A consumer survives a transient database failure

**Feature Branch**: `fix/299-transient-retry`
**Created**: 2026-10-03
**Status**: Draft
**Issue**: #299
**Input**: the load test of #290 found a paid order whose stock confirmation went to Inventory's error queue on a
`40001` serialization failure, with no retry. Its units would return to the shelf at the hold's expiry.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A paid order keeps its stock under load (Priority: P1)

Many customers buy one product at a steady rate. Every paid order's reservation is confirmed, and none is left held
for the expiry sweeper to put back on the shelf.

**Why this priority**: a confirmation lost to a transient failure resells a unit somebody paid for.

**Independent Test**: `server/loadtest/run.sh checkout` holds its invariants (units sold = units deducted, nothing
held), and every `_error` queue is empty afterwards.

**Acceptance Scenarios**:

1. **Given** a consumer whose transaction PostgreSQL aborts with `40001` (serialization) or `40P01` (deadlock),
   **When** the message is consumed, **Then** it is tried again in a fresh transaction and succeeds, with no fault.
2. **Given** a consumer that fails for a reason that is not transient (a bug, a validation failure), **When** it is
   consumed, **Then** it is **not** retried. It faults at once, as today, so a real error is not delayed or hidden.
3. **Given** a transient failure that persists past the retries, **When** they run out, **Then** the message goes to
   its `_error` queue, as today, and nothing is lost.

### Edge Cases

- **A retry after a partial success**: impossible, because each attempt is one transaction that rolled back. The
  inbox and the guarded updates make a redelivery of a message that did commit change nothing (Principle III).
- **A message published inside a failed attempt**: the outbox discards it with the rolled-back transaction. Only the
  attempt that commits publishes.

## Requirements *(mandatory)*

- **FR-001**: One retry policy in `Ecommerce.Shared`, applied to every receive endpoint of every service. It sits
  outside the EF outbox, so each attempt has a transaction of its own.
- **FR-002**: Retry only transient database failures: `40001`, `40P01`, and a connection the driver calls transient.
- **FR-003**: Several attempts with growing, jittered intervals, so a crowd of losers does not collide again in step.
- **FR-004**: Identity and Cart, which have no EF outbox callback, get the retry too.

## Success Criteria *(mandatory)*

- **SC-001**: The checkout load run passes with every `_error` queue empty.
- **SC-002**: A test consumer failing with `40001` twice is consumed once with no fault; one failing with any other
  exception is not retried.

## Assumptions

- MassTransit 8.3: `UseMessageRetry` configured before `UseEntityFrameworkOutbox` wraps it.
