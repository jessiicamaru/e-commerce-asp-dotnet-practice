# Feature Specification: Checkouts of one product do not queue behind retries

**Feature Branch**: `fix/301-inventory-read-committed`
**Created**: 2026-10-03
**Status**: Draft
**Issue**: #301
**Input**: with #299's retry in place, the checkout load run is correct but slow on one product:
- median settle time 8.3 s (1.0 s before), p99 40.3 s;
- 33 checkouts dropped;
- 7,648 serialization failures in Inventory's log in two minutes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A popular product sells as fast as a quiet one (Priority: P1)

Many customers buy the same product at a steady rate. Each order settles in about the time the saga takes, not in a
queue of retries, and the stock stays exactly right.

**Why this priority**: one popular product is the normal shape of a sale. Checkout should not slow to a crawl on it.

**Independent Test**: `server/loadtest/run.sh checkout`. Expected:
- every invariant held;
- every `_error` queue empty;
- no storm of serialization failures in Inventory's log;
- the settle time back near the pre-#299 median.

**Acceptance Scenarios**:

1. **Given** steady checkouts of one product, **When** the run ends, **Then**:
   - units sold equal units deducted;
   - nothing is held or stuck;
   - the settle time's median is of the same order as before #299.
2. **Given** 100 customers racing for 20 units, **When** it ends, **Then** exactly 20 are paid: the race still holds.

### Edge Cases

- **A redelivered message**: the inbox still recognises it, by its unique key.
- **A deadlock or a lost connection**: still retried by #299's policy, which stays.

## Requirements *(mandatory)*

- **FR-001**: Inventory's consumes run in `ReadCommitted`. That is the isolation its handlers already run in, and are
  tested in, everywhere else.
- **FR-002**: #299's transient retry stays.
- **FR-003**: Other services are unchanged until measured.

## Success Criteria *(mandatory)*

- **SC-001**: The checkout run passes with its settle-time median near 1 s, and the race passes three times.
- **SC-002**: The Inventory test suite passes unchanged.

## Assumptions

- MassTransit 8.3's EF outbox takes `IsolationLevel` in its configuration, defaulting to `RepeatableRead`.
