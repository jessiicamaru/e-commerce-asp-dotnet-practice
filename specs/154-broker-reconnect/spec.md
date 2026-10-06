# Feature Specification: Services reconnect to the broker within seconds of its return

**Feature Branch**: `fix/304-broker-reconnect`
**Created**: 2026-10-06
**Status**: Draft
**Issue**: #304
**Input**: after a RabbitMQ outage, nothing is lost (specs/147), but orders wait long after the broker is back. Over
four recorded runs of `fault.sh broker` (about 60 s down), the backlog cleared **51–99 s after recovery**, and orders
placed *after* recovery waited a median of **29–78 s** (0.2 s before the fault).

> **Corrected premise** (2026-10-06, after the first runs with the fix). "Backlog cleared after recovery" was counted
> from `docker start`. The broker's own log shows RabbitMQ then takes **~31 s to open port 5672**. Its compose health
> check (`rabbitmq-diagnostics ping`) passed ~23 s before that, so "recovered" and "healthy" were both early. With the
> fix, every service was reconnected **5.8 s after the port opened**. The rest of the backlog time was RabbitMQ's boot
> and draining the backlog itself, about 264 orders of saga messages on a laptop.
>
> So this feature claims what it can show:
> - the reconnect lag, measured from the moment the broker accepts connections, A/B against MassTransit's own schedule
>   on the same stack;
> - a health check and a harness that measure from that moment.
>
> The 25 s backlog criterion is withdrawn: it measured the broker's boot.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Checkout resumes within seconds of the broker's return (Priority: P1)

When RabbitMQ comes back, every service reconnects within a few seconds. The outboxes start delivering, and a
customer who orders right after recovery waits about as long as on a normal day plus the backlog ahead of them, not
an extra half-minute per missed reconnect attempt.

**Why this priority**: a broker restart is the most ordinary outage there is (an upgrade, a host reboot). The shop
should be back at full speed as soon as the broker is.

**Independent Test**: `fault.sh broker` three times with the fix on and three times off
(`Messaging__ReconnectQuickly=false`), same stack. Read from the broker's own log, the time from RabbitMQ accepting
connections to every service's first connection is clearly shorter with it on. Every invariant holds and every error
queue is empty in all six runs.

**Acceptance Scenarios**:

1. **Given** RabbitMQ down for 60 s under steady checkouts, **When** it opens its port again, **Then**:
   - every service has reconnected within about 6 s;
   - nothing is lost and no error queue holds a message.
2. **Given** the broker restarting, **When** compose, the deploy's `--wait` or the harness asks whether it is healthy,
   **Then** the answer is yes only once it accepts connections.
3. **Given** RabbitMQ down for a long time, **When** services keep trying, **Then** each receive endpoint tries at
   most once every 5 s: a few connection attempts a second across the stack, which costs the broker nothing.
4. **Given** a MassTransit release whose internals differ, **When** the change cannot apply, **Then** the service
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
- **FR-004**: `Messaging:ReconnectQuickly=false` keeps MassTransit's schedule, with a log line saying so: the A/B
  baseline, and the way back without a release.
- **FR-005**: The broker's compose health check passes only when it accepts connections (`check_port_connectivity`).
- **FR-006**: `fault.sh broker` records when the broker opened its port (`ready_at`, from its log), each service's
  reconnect lag after that (`reconnected_after_ready_s`, from the connections the broker accepted), and the backlog
  cleared after `ready_at`.

## Success Criteria *(mandatory)*

- **SC-001**: In three runs with the fix, every service reconnects within 6 s of the broker opening its port. In three
  runs without it, on the same stack, the slowest service takes clearly longer, as MassTransit's 3-30 s schedule
  predicts. All six runs pass with every error queue empty.
- **SC-003**: `docker compose up --wait` and the health check report the broker healthy only after its port is open.
- **SC-002**: The tests show the policy applied to a real RabbitMQ bus configuration, handling what MassTransit's did,
  and a mutation that skips it is caught.

## Assumptions

- MassTransit 8.3 is pinned: Dependabot never proposes 9 (specs/153), and a minor that changes these internals fails
  the test.
