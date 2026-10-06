# Feature Specification: MassTransit back on 8.3.6 after a broker outage stopped consumers for good

**Feature Branch**: `fix/353-pin-masstransit`
**Created**: 2026-10-06
**Status**: Draft
**Issue**: #353
**Input**: on MassTransit **8.5.11** (main since the Dependabot group #326), stopping RabbitMQ under steady checkouts
left receive endpoints **stopped for good**: Inventory's `ReserveInventory` in two runs (716 messages, 0 consumers, 713
orders `Submitted`), Order's `OrderSvcOrderCompleted`, `OrderSvcOrderFailed` and `OrderSvcEraseAccountFromOrders` in a
third (311 orders `Submitted`) - until the service was restarted. 3 of 3 runs on 8.5.11, 0 of 7 on 8.3.6. The third run
had specs/154's reconnect policy switched off, so it is not the cause. Decided with the user: pin MassTransit to 8.3.6
and stop Dependabot proposing its minors until the cause is understood.

> **Amended** (2026-10-06, after #354 merged): Dependabot at once proposed the **8.3.7 patch** (#357), which the first
> rule let through. CI would have passed it and could not have shown anything about #353, which lies somewhere after
> 8.3.6. Decided with the user: close it and pin **exactly 8.3.6** - every MassTransit update ignored, the test exact.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Checkout keeps going after the broker comes back (Priority: P1)

When RabbitMQ restarts under load, every service's consumers come back with it. Orders placed during and after the
outage settle without anybody restarting a service.

**Why this priority**: a broker restart is ordinary (an upgrade, a host reboot). On 8.5.11 one silently stopped
checkout until somebody noticed the queue and restarted the service: nothing lost, but nothing sold either.

**Independent Test**: `loadtest/fault.sh broker` at least three times on the stack built from this branch. Each passes:
every order terminal, stock consistent, every error queue empty, and no queue left with messages and no consumer.

**Acceptance Scenarios**:

1. **Given** the stack on MassTransit 8.3.6 and RabbitMQ stopped for 60 s under steady checkouts, **When** it returns,
   **Then** every queue has its consumers again and every order settles, with no service restarted.
2. **Given** Dependabot's weekly run, **When** MassTransit publishes any version, a patch included, **Then** no pull
   request proposes it.
3. **Given** somebody bumps a MassTransit reference by hand, **When** CI builds it, **Then** a test fails and names #353.

### Edge Cases

- **A MassTransit 8.3.x patch**: not proposed either (amended). Nothing in CI can say whether it carries the regression,
  and a security fix is not missed: a vulnerable package fails the build (specs/150), which is when to test a version
  with `fault.sh broker` and move.
- **Specs/154's reconnect policy** sets a private field of 8.3.6's RabbitMQ host. It was written and measured on 8.3.6
  and `BrokerReconnectTests` reads it back, so going back is going to the version it was designed against.

## Requirements *(mandatory)*

- **FR-001**: Every `MassTransit*` package reference in the solution is 8.3.6.
- **FR-002**: Dependabot ignores every MassTransit update, with the reason in the file.
- **FR-003**: A test fails when any project references a MassTransit version other than 8.3.6, or the build resolves one.
- **FR-004**: The 25 other package updates of #326 stay as they are.

## Success Criteria *(mandatory)*

- **SC-001**: Three or more `fault.sh broker` runs on this branch pass, and after each no queue is left holding
  messages with no consumer.
- **SC-002**: Every test passes on 8.3.6, `BrokerReconnectTests` included.
- **SC-003**: `MassTransitVersionTests` fails with a version other than 8.3.6, 8.3.7 included (checked by mutation).

## Assumptions

- The root cause in 8.5.11 is not established here (issue #353 keeps the leads: `ReceiveTransportAgent.Run` no longer
  rethrowing an `OperationCanceledException`). Finding it is the condition for moving off 8.3, not part of this change.
- No 8.4/8.5-only API is used: the 8.5.11 bump changed no code.
