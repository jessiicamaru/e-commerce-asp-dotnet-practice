# Feature Specification: Load-test checkout and measure it

**Feature Branch**: `feat/290-load-tests`
**Created**: 2026-10-03
**Status**: Draft
**Issue**: #290
**Input**: "The tests prove the rules (no overselling, guarded updates) but give no numbers: how many checkouts a second,
at what latency, and whether the guarantees hold under real concurrency through the saga."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Many customers race for the last units (Priority: P1)

A scarce variant has 20 units, and 100 customers each check out one at the same time. Exactly 20 orders are paid, 80
fail for lack of stock, the stock reads 0 on hand with nothing held, and no order is left `Submitted`.

**Why this priority**: overselling is the failure a shop cannot afford. The integration tests prove the row lock in
one service; this proves it through the gateway, the saga and five services at once.

**Independent Test**: `server/loadtest/run.sh race`. It ends with the consistency check passing, or names the
assertion that failed.

**Acceptance Scenarios**:

1. **Given** 20 units and 100 single-unit checkouts started together, **When** every order has settled, **Then**:
   - 20 are `Paid` and 80 `Failed`;
   - on hand is 0 and reserved is 0;
   - none is `Submitted`.
2. **Given** the same run, **When** it ends, **Then** units sold equal units deducted: paid quantity = starting stock
   - on hand.

---

### User Story 2 - Customers check out at a steady rate (Priority: P1)

Customers with plenty of stock add to cart, ask for a quote and place orders, at a steady arrival rate. The run reports
throughput and latency per step, and the time each order takes to settle through the saga.

**Why this priority**: the evaluation chapter needs numbers for the path that matters.

**Independent Test**: `server/loadtest/run.sh checkout` writes a summary with checkouts per second, p50/p95/p99 per
request, the settle time, and the error rate. The consistency check passes afterwards.

---

### User Story 3 - Shoppers browse the catalogue (Priority: P2)

Anonymous shoppers list, search and open products, ramping up. The run reports throughput and latency for the read
path, with and without the search filter.

**Independent Test**: `server/loadtest/run.sh browse`.

### Edge Cases

- **Rate limits**: the gateway limits sign-in and registration per client address (specs/062), so a load generator on
  one address cannot register 100 customers through it. Setup registers them through Identity directly; the measured
  traffic goes through the gateway.
- **Staff sign-in needs a code** (specs/110): setup computes the administrator's TOTP code and exchanges it with the
  back office's `Origin` (specs/138), like every tool that acts as staff.
- **Leftovers**: every run removes the products and category it created, as the verify scripts do (specs/073).
- **The machine**: numbers from a laptop running every container are not production numbers. The report names the
  machine and says so.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Three k6 scenarios (`browse`, `checkout`, `race`) under `server/loadtest/`, run by one command against
  the compose stack.
- **FR-002**: k6 runs from its official container image on the compose network: nothing to install, the same on any
  machine.
- **FR-003**: Each scenario prepares its own data (category, product, stock, customers, addresses) and removes the
  catalogue data afterwards.
- **FR-004**: `checkout` and `race` finish with a consistency check through the API. Every order settled, units sold
  equal units deducted, nothing held. A failure makes the run fail.
- **FR-005**: Each run writes a JSON summary. The report in `docs/testing/` is built from summaries kept in the
  repository, with the machine and settings named.
- **FR-006**: Thresholds make a run fail on errors (unexpected status codes) or on a broken invariant, not on
  latency: latency is reported, not asserted, because it depends on the machine.

## Success Criteria *(mandatory)*

- **SC-001**: `race` sells exactly the stock, three runs in a row.
- **SC-002**: `checkout` and `browse` complete with no unexpected errors, and their numbers are in the report.
- **SC-003**: Every number in the report comes from a summary file in the repository.

## Assumptions

- The compose stack is running (`docker compose -f docker-compose.yml -f docker-compose.app.yml up -d`) with the stub
  provider approving.
- The administrator's TOTP secret is the development one (`ADMIN_TOTP_SECRET`), as for the verify scripts.
