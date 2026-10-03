# Feature Specification: Checkout survives a service or the broker going down

**Feature Branch**: `feat/291-resilience`
**Created**: 2026-10-04
**Status**: Draft
**Issue**: #291
**Input**: "The outbox, the saga's timeout and the compensations are designed for failure, but nothing shows them
recovering from one in the running system."

## User Scenarios & Testing *(mandatory)*

Every scenario follows the same pattern:
1. Customers check out at a steady rate.
2. One part of the system fails for a while, then comes back.
3. Afterwards:
   - every order has reached `Paid` or `Failed`;
   - units sold equal units deducted, and nothing is held;
   - every `_error` queue is empty: no message was lost or faulted.
4. The run records:
   - when the fault started and ended;
   - what customers saw while it lasted;
   - how long after recovery the last order settled.

### User Story 1 - Payment stops answering (Priority: P1)

Payment is stopped for a minute while customers keep checking out. Orders are placed and their stock reserved. The
saga keeps them waiting, because the payment timeout (specs/053) is longer than the outage. When Payment returns it
charges what queued up, and every order settles to Paid.

**Why this priority**: the payment provider is the dependency most likely to be slow or down in production.

**Independent Test**: `server/loadtest/fault.sh payment`. Expected:
- every order Paid, invariants held;
- the last order settled within seconds of Payment's return.

### User Story 2 - The broker goes down (Priority: P1)

RabbitMQ is stopped for a minute. Checkout still accepts orders: Order writes them, with their `OrderSubmittedEvent`,
to its own database through the transactional outbox. Nothing moves until the broker returns. Then every service
reconnects, the outboxes deliver, and every order settles. Nothing is lost.

**Why this priority**: the transactional outbox exists for exactly this, and nothing has shown it working.

**Independent Test**: `server/loadtest/fault.sh broker`. Expected:
- every order placed during the outage settles after it;
- invariants held;
- error queues empty.

### User Story 3 - The orchestrator restarts (Priority: P1)

The orchestrator is restarted in the middle of checkouts. Sagas in flight are stored in its database, and their next
messages wait in their queues. Every order still settles.

**Independent Test**: `server/loadtest/fault.sh orchestrator`. Expected: every order settles, invariants held.

### User Story 4 - Inventory hangs (Priority: P2)

Inventory is frozen for 30 seconds (`docker pause`): it holds its connections but answers nothing. This is the
behaviour of a stalled process, not a dead one. Reservations queue in the broker, and when it resumes every order
settles.

**Independent Test**: `server/loadtest/fault.sh inventory`. Expected: every order settles, invariants held.

### Edge Cases

- **An outage longer than the payment timeout** (600 s): the saga fails the order and releases its stock. That path
  is specs/053's, covered by the orchestrator's tests and not repeated here.
- **Customer-facing errors during a fault**: recorded, not hidden. A run fails on a broken invariant, not on an error
  a customer saw while something was down; the report says how many there were.

## Requirements *(mandatory)*

- **FR-001**: One command per fault: `server/loadtest/fault.sh payment|broker|orchestrator|inventory`.
- **FR-002**: The fault is injected while a k6 checkout load runs, then removed. The load keeps arriving through the
  fault.
- **FR-003**: After the load, the run waits for every order to settle, then checks the invariants through the API
  and the error queues through the broker.
- **FR-004**: Each run keeps a summary and a fault timeline. A generated report in `docs/testing/` gives, per
  scenario:
  - orders placed;
  - errors customers saw;
  - the fault window;
  - the time from recovery to the last order settled;
  - whether the invariants held.
- **FR-005**: No code in the services changes to make a scenario pass, unless the scenario finds a defect. A defect
  gets its own issue, as #299 and #301 did.

## Success Criteria *(mandatory)*

- **SC-001**: All four scenarios pass: every order terminal, invariants held, error queues empty.
- **SC-002**: The report states each scenario's recovery time and customer-facing errors, from kept files.

## Assumptions

- The compose stack is running with the stub approving, as for the load tests (specs/144).
- Docker controls the containers: `docker stop`, `docker start`, `docker restart`, `docker pause`.
