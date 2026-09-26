# Research: Saga payment timeout

> Written on 2026-09-27, after the feature merged (#135), from the code at that merge, the pull request and
> docs/architecture/saga-orchestration-roadmap.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

Six decisions. D1, D3 and D4 are the ones the pull request records as decided on the user's behalf.

---

## D1 - A sweeper over the saga table, not MassTransit's `Schedule`

**Decision**: `PaymentTimeoutSweeper`, a `BackgroundService` with a `PeriodicTimer`, reads
`order_state_data` for instances in `InventoryReservedState` whose `UpdatedAt` is before `now - timeout`, and
publishes `PaymentTimeoutExpired` for each.

**Rationale**: The saga table already knows who is waiting and since when. The shape is the one Inventory's
`ReservationExpirySweeper` and Order's `DeliveryConfirmationSweeper` already have: a scope per tick, and one bad
tick never ends it. Several instances are safe because a second announcement is ignored by state (D5).

**Alternatives considered**:

- **MassTransit `Schedule` with durable scheduling.** Rejected: it needs RabbitMQ's delayed-message plugin or a
  Quartz or Hangfire store, and compose and CI run stock `rabbitmq:3-management` with neither. Specs/001 research
  D4 rejected the same thing for the same reason.
- **The in-memory scheduler.** Rejected: it forgets every pending timeout on restart - exactly when a stuck
  payment is most likely.

---

## D2 - The deadline is measured from `UpdatedAt` on entering the wait

**Decision**: The due query compares `UpdatedAt`, which the `InventoryReserved` transition sets, and nothing
in `InventoryReservedState` writes it again.

**Rationale**: The saga's clock and Inventory's start at the same moment, give or take a message hop. No new
column is needed.

**Alternatives considered**: a dedicated `PaymentRequestedAt` column is not recorded as considered; the
existing column was sufficient and needed no migration.

---

## D3 - The timeout plus one sweep must be shorter than the hold, checked at startup

**Decision**: Default 600 s timeout and 30 s sweep against Inventory's 15-minute hold. `PaymentTimeoutOptions.From`
throws at startup - naming every problem - when either value is not a positive whole number of seconds, when
the hold is not a positive number of minutes, or when `timeout + sweep >= hold`. The hold is checked only when
it is visible (the same `.env`; compose now passes `INVENTORY_RESERVATION_TTL_MINUTES` to the orchestrator).

**Rationale**: The order must fail while its stock is still held, so `ReleaseInventoryCommand` returns it at
once and nothing is paid for after it went back on the shelf. The pull request: "the 5-minute margin is well
over one 30-second sweep".

**Alternatives considered**: none recorded beyond the default values themselves.

---

## D4 - A late approval is refunded, not completed

**Decision**: In `PaymentTimedOut`, `PaymentProcessed` publishes `RefundPaymentCommand(OrderId, Reason)` and
finalises; `PaymentFailed` just finalises.

**Rationale**: Payment refunds what its own row says it took, once (`refunds.OrderId` unique, specs/039), so the
command carries no amount.

**Alternatives considered**:

- **Complete the order anyway.** Rejected in the pull request: it would mean re-reserving stock that may already
  be sold.

---

## D5 - Repeats and strays are nothing

**Decision**: `Ignore(PaymentTimeout)` in `PaymentTimedOut`; `OnMissingInstance(m => m.Discard())` for the
timeout event.

**Rationale**: Two sweepers, or two ticks, may announce one order; the second must not fault
(`A_repeated_timeout_fails_the_order_once` asserts it is not faulted). A timeout finding no instance means the
payment answered and the order finished first - its ordinary fate, so it gets no Warning, unlike the saga's other
events, whose missing instance is the symptom of the 2026-09-21 stall (feature 013).

**Alternatives considered**: none recorded.

---

## D6 - Contracts: one public, one private

**Decision**: `RefundPaymentCommand` in `Ecommerce.Contracts/Payment`; `PaymentTimeoutExpired` in the
orchestrator's `Timeouts/` folder. Payment's consumer is `RefundLatePaymentConsumer`, and `RefundOrderCommand`
gains `Reason` with the default `RefundOrderCommand.Cancelled` ("the order was cancelled").

**Rationale**: A record in Contracts states that one service tells another something (CLAUDE.md); the timeout
is the orchestrator talking to itself. The consumer is named for what it does because a consumer's class name
becomes its queue name (CLAUDE.md gotcha) - a second `...Consumer` of refunds must not share
`RefundCancelledOrderConsumer`'s queue. The default reason keeps specs/039's consumer unchanged.

**Alternatives considered**: none recorded.

---

## Found along the way

Recorded in the pull request as mistakes in the author's own checks, not in the feature:

- A Mermaid checker had been skipping every document whose working copy has Windows line endings; fixed, it
  checked all 20 diagrams, all valid.
- `NotExists` in MassTransit's harness returns `null` when the instance is gone; the first assertion expected the
  opposite.
