# Feature Specification: Inventory consumes without serialization aborts

**Feature Branch**: `fix/301-inventory-read-committed`
**Created**: 2026-10-03
**Status**: Draft
**Issue**: #301
**Input**: with #299's retry in place, the checkout load run is correct, but Inventory logs thousands of `40001`
serialization aborts in two minutes: 7,648, and 5,610 in a warm run.

> **Corrected premise.** The issue first blamed the aborts for a slow median: 8.3 s against 1.0 s. That run came
> straight after every container was rebuilt, so it was a cold start. A per-stage breakdown from each service's own
> timestamps showed identical code swinging between a 0.3 s and a 10 s median from run to run. The slow stages are
> always Inventory's reservation and confirmation, waiting on the one stock row every checkout of one product
> shares. This feature therefore claims what it can show: no aborts. Latency is judged over several warm runs, not
> one.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A popular product is reserved without aborting and retrying (Priority: P1)

Many customers buy the same product at a steady rate. Each reservation and confirmation waits its turn on the stock
row and then proceeds. None is aborted and retried, and the stock stays exactly right.

**Why this priority**: an abort is work done and thrown away, on the busiest row in the shop. Under
`REPEATABLE READ`, every concurrent consume of a popular product is aborted once for each consume ahead of it.

**Independent Test**: `server/loadtest/run.sh checkout`. Expected:
- every invariant held;
- every `_error` queue empty;
- no serialization failure in Inventory's log;
- latency, over several warm runs, no worse than at `REPEATABLE READ`.

**Acceptance Scenarios**:

1. **Given** steady checkouts of one product, **When** the run ends, **Then**:
   - units sold equal units deducted;
   - nothing is held or stuck;
   - Inventory logs no `40001`.
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

- **SC-001**: The checkout run passes with no `40001` in Inventory's log, and the race passes three times.
- **SC-003**: Over several warm checkout runs, created-to-paid latency at `READ COMMITTED` is no worse than at
  `REPEATABLE READ`.
- **SC-002**: The Inventory test suite passes unchanged.

## Assumptions

- MassTransit 8.3's EF outbox takes `IsolationLevel` in its configuration, defaulting to `RepeatableRead`.
