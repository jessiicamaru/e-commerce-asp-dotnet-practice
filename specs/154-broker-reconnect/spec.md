# Feature Specification: Services reconnect to the broker within seconds of its return

**Feature Branch**: `fix/304-broker-reconnect`
**Created**: 2026-10-06
**Status**: Draft
**Issue**: #304
**Input**: after a RabbitMQ outage, nothing is lost (specs/147), but orders wait long after the broker is back. Over
four recorded runs of `fault.sh broker` (about 60 s down), the backlog cleared **51–99 s after recovery**, and orders
placed *after* recovery waited a median of **29–78 s** (0.2 s before the fault).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Checkout resumes within seconds of the broker's return (Priority: P1)

When RabbitMQ comes back, every service reconnects within a few seconds. The outboxes start delivering, and a
customer who orders right after recovery waits about as long as on a normal day plus the backlog ahead of them, not
an extra half-minute per missed reconnect attempt.

**Why this priority**: a broker restart is the most ordinary outage there is (an upgrade, a host reboot). The shop
should be back at full speed as soon as the broker is.

**Independent Test**: `fault.sh broker`, several runs. The backlog clears clearly sooner after recovery than in the
four baseline runs, with every invariant still holding and every error queue empty.

**Acceptance Scenarios**:

1. **Given** RabbitMQ down for 60 s under steady checkouts, **When** it returns, **Then**:
   - every service's bus is healthy again within about 5 s of the broker accepting connections;
   - the backlog clears in well under the baseline's 51–99 s;
   - nothing is lost and no error queue holds a message.
2. **Given** RabbitMQ down for a long time, **When** services keep trying, **Then** each receive endpoint tries at
   most once every 5 s: a few connection attempts a second across the stack, which costs the broker nothing.
3. **Given** a MassTransit release whose internals differ, **When** the change cannot apply, **Then** the service
   still starts, with MassTransit's own default, and the test that checks the change fails in CI.

### Edge Cases

- **Wrong credentials**: never retried quickly. MassTransit ignores `AuthenticationFailureException` and so does this
  policy, because a fast loop of failed sign-ins helps nobody.
- **A broker that accepts connections but is still starting** (its health check not yet passing): the next attempt is
  at most 5 s later, not 30.
- **Several instances of a service**: each reconnects on its own schedule, with jitter, so they do not stampede.

## Requirements *(mandatory)*

- **FR-001**: Every service's RabbitMQ transport retries a lost connection with a growing interval capped at 5 s, in
  place of MassTransit 8.3's hard-coded 3–30 s, handling exactly the same exceptions.
- **FR-002**: It is one call in the shared building block, made in every service's `UsingRabbitMq`.
- **FR-003**: If it cannot apply, the service starts with the default and says so in its log. A test fails in CI in that
  case.

## Success Criteria *(mandatory)*

- **SC-001**: Over three `fault.sh broker` runs, the backlog clears after recovery in at most half the baseline's best
  (51 s), so 25 s or less, with every invariant held and every error queue empty.
- **SC-002**: The tests show the policy applied to a real RabbitMQ bus configuration, handling what MassTransit's did,
  and a mutation that skips it is caught.

## Assumptions

- MassTransit 8.3 is pinned: Dependabot never proposes 9 (specs/153), and a minor that changes these internals fails
  the test.
