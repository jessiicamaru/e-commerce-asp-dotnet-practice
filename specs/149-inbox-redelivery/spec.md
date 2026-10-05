# Feature Specification: A message delivered twice at once is consumed once and faults neither time

**Feature Branch**: `fix/306-inbox-redelivery`
**Created**: 2026-10-05
**Status**: Draft
**Issue**: #306
**Input**: during #292's broker-fault run (`fault.sh broker`, RabbitMQ down for 65 s), a `ProcessPaymentCommand`
faulted into `ProcessPayment_error`. Its order had been paid, so a person would have had to find a message that looks
exactly like a lost payment and decide it was not one. The fault was
`23505: duplicate key value violates unique constraint "AK_InboxState_MessageId_ConsumerId"`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A duplicate delivery is dropped, not faulted (Priority: P1)

After a broker outage, the same message can arrive twice while the first copy is still being consumed. Two cases:
- a delivery that was never acknowledged is redelivered on the new connection;
- an outbox's delivery is repeated because its first send was never marked done.

The consumer runs once. The second copy is recognised as a duplicate and dropped. Nothing reaches an `_error` queue,
and nobody has to look.

**Why this priority**: an `_error` queue is where a lost payment or lost stock confirmation would be. A false alarm
there teaches operators to purge it without reading. After #299 and #301, this was the one fault the resilience runs
could still produce.

**Independent Test**: one message, two deliveries at the same moment, through the real EF outbox and inbox on
PostgreSQL. The consumer runs once and neither delivery faults.

**Acceptance Scenarios**:

1. **Given** a message delivered twice at once, **When** both deliveries finish, **Then**:
   - the consumer has run exactly once;
   - neither delivery faulted.
2. **Given** a unique violation on any other constraint, **When** it happens, **Then** it faults at once, as before. A
   duplicate of the shop's own data is a refusal, not a race.
3. **Given** the broker stopped during steady checkouts (`fault.sh broker`), **When** the run ends, **Then** every
   `_error` queue is empty.

### Edge Cases

- **The first consume fails after the duplicate was dropped.** Not possible: the duplicate waits on the first
  transaction's inbox row and is tried again only after that transaction has ended. If it rolled back, the retry finds no
  row, inserts its own, and consumes the message. Nothing is lost either way.
- **Three or more copies.** Each is retried the same way, and each finds the row consumed.
- **The inbox key renamed** by a future MassTransit migration. The test drives the real inbox and asserts on what
  happens, so a rename fails the test instead of passing silently.

## Requirements *(mandatory)*

- **FR-001**: A unique violation on the inbox's own key (`AK_InboxState_MessageId_ConsumerId`) is retried by the
  transient policy of specs/145.
- **FR-002**: Every other unique violation faults at once.
- **FR-003**: The policy stays in `Ecommerce.Shared/Messaging`, so every service that consumes gets the change with no
  edit of its own.

## Success Criteria *(mandatory)*

- **SC-001**: In the test, two simultaneous deliveries of one message give one consume and zero faults, and the same
  test without the policy shows the fault.
- **SC-002**: `fault.sh broker` passes with every `_error` queue empty.
- **SC-003**: The Inventory, Payment and Order suites pass.

## Assumptions

- Every service's inbox key has the same name, created by MassTransit's `AddTransactionalOutboxEntities()`: checked in
  all seven migrations.
- MassTransit's inbox, when it finds its row already consumed, acknowledges the message without running the consumer.
  This is its documented purpose, and the test shows it happening.
