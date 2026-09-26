# Implementation Plan: Saga payment timeout

> Completed on 2026-09-27, after the feature merged (#135), from the code at that merge, the pull request and
> docs/architecture/saga-orchestration-roadmap.md.

**Branch**: `053-saga-payment-timeout` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #123

## Summary

Give the saga a deadline for Payment that ends before Inventory's hold does. A `PaymentTimeoutSweeper` in the
orchestrator reads `order_state_data` for orders in `InventoryReservedState` older than the timeout and
publishes `PaymentTimeoutExpired` to the saga through its outbox. The saga releases the stock, publishes
`OrderFailedEvent`, and waits in a new `PaymentTimedOut` state: a late approval sends the new
`RefundPaymentCommand`, which Payment's `RefundLatePaymentConsumer` turns into the once-only refund of
specs/039; a late rejection needs nothing. The orchestrator refuses to start if the timeout plus one sweep is
not shorter than the hold. `Ecommerce.Orchestrator.Tests` is the saga's first test project.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MassTransit 8.3.6 (state machine, EF Core saga repository, bus outbox, test harness),
MediatR 12.4.1 (Payment), Npgsql EF Core, `TimeProvider`

**Storage**: PostgreSQL - `ecommerce_saga_db` (5436, `order_state_data`, no schema change) and
`ecommerce_payment_db` (5438, `refunds`, no schema change)

**Testing**: xUnit; the state machine through `AddMassTransitTestHarness`; the sweeper's query and the outbox
publish against a real PostgreSQL on 5436; Payment's refund against PostgreSQL on 5438; an end-to-end run on
the compose stack; mutations

**Target Platform**: Orchestrator (5058) and Payment (5061), in containers or with `start-dev`

**Performance Goals**: none; a sweep reads at most `BatchSize` = 200 ids

**Constraints**: the timer must survive a restart and tolerate several instances; every publish through the
outbox; timeout + sweep < hold

**Scale/Scope**: one new state, three new transitions, one sweeper, one contract, one consumer

## Design

**State machine (`OrderStateMachine`)**
- **New state `PaymentTimedOut`.** A new event, `PaymentTimeoutExpired(OrderId)`. It is internal to the
  orchestrator, in its own assembly rather than in Contracts, because no other service publishes or
  consumes it.
- **In `InventoryReservedState`:**
  - on `PaymentTimeoutExpired`: record the reason; publish `ReleaseInventoryCommand` and
    `OrderFailedEvent`; transition to `PaymentTimedOut`.
- **In `PaymentTimedOut`:**
  - on `PaymentProcessed`: publish `RefundPaymentCommand`, then finalize;
  - on `PaymentFailed`: finalize, since nothing was taken;
  - a repeated `PaymentTimeoutExpired` is ignored.
- **A timeout that finds no instance** (the order already completed) is discarded, at Debug. That is
  its expected fate, not a warning.

> **Correction (2026-09-27, from the code at the merge):** the missing-instance case is
> `x.OnMissingInstance(m => m.Discard())` with no log line at all, not a Debug entry. The other events keep
> their Warning for a missing instance (feature 013).

**Sweeper (`PaymentTimeoutSweeper`)**
- It is shaped like Inventory's `ReservationExpirySweeper`: a `PeriodicTimer`, one scope per tick, and
  one bad tick never ends it.
- Each tick asks `PaymentTimeouts.DueAsync(context, cutoff)` for the ids of instances in
  `InventoryReservedState` whose `UpdatedAt` is older than the cutoff. It publishes one
  `PaymentTimeoutExpired` for each, then calls `SaveChangesAsync`, so the bus outbox carries them.
- `UpdatedAt` is set when the saga enters `InventoryReservedState`, which is the moment Inventory's hold
  also starts counting.

At the merge `DueAsync` orders by `UpdatedAt` and takes at most `PaymentTimeouts.BatchSize` (200). The state
it filters on is the constant `PaymentTimeouts.AwaitingPayment = nameof(OrderStateMachine.InventoryReservedState)`,
which is the name as stored, and `The_state_the_sweeper_looks_for_is_the_one_the_saga_stores` holds the two
together. A tick that announces anything logs a Warning with the count.

**Configuration (`PaymentTimeoutOptions`)**
- `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS` (default 600) and `ORCHESTRATOR_TIMEOUT_SWEEP_SECONDS`
  (default 30), both validated at startup.
- If `INVENTORY_RESERVATION_TTL_MINUTES` is visible, as it is from the same `.env` or compose, the
  timeout plus one sweep interval must be shorter than the hold. Otherwise the orchestrator refuses to
  start, naming both values.

**Payment**
- `RefundPaymentCommand(OrderId, Reason)` in Contracts/Payment.
- `RefundLatePaymentConsumer`, named for what it does (CLAUDE.md's queue-name gotcha), sends
  `RefundOrderCommand(OrderId, Reason)`.
- `RefundOrderCommand` gains an optional reason, defaulting to the cancelled-order wording, so specs/039's
  consumer is unchanged.

## Why a sweeper and not `Schedule`

MassTransit's durable scheduling needs either RabbitMQ's delayed-message plugin or a Quartz or Hangfire
store. Compose and CI run stock `rabbitmq:3-management`. The in-memory scheduler loses every pending
timeout on restart, which is exactly when a stuck payment is most likely. This codebase already has two
sweepers of this shape.

## Why the margin

The saga's clock starts when it enters `InventoryReservedState`; Inventory's starts when it holds the
stock. They are the same moment, give or take a message hop. Timeout plus one sweep interval shorter
than the hold means the saga fails the order while the stock is still held. `ReleaseInventoryCommand`
then returns it at once, rather than 15 minutes later.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md); re-checked after design.*

- **III (atomic writes, idempotent messaging).**
  - The sweeper publishes through the orchestrator's outbox.
  - Duplicate timeouts are ignored by state.
  - The refund is once-only by `refunds.OrderId` being unique (specs/039).
  - Pass.
- **V (evidence).**
  - State machine tests through the harness, written before the transitions.
  - Sweeper tests against PostgreSQL.
  - Payment tests.
  - A manual end-to-end run with Payment stopped past the timeout.
  - Mutation checks.
  - Pass.
- **Rollback.**
  - `PaymentTimedOut` is a new state value in `order_state_data.CurrentState`, which is text, not an
    enum. No schema changes.
  - An older orchestrator would fail to load an instance in that state only when a late answer arrives.
    That message goes to the error queue, not a silent loss.
  - Recorded as a known limit.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The orchestrator reads only its own saga table; Payment refunds from its own `payments` row, so the command carries no amount; Inventory is told to release, not reached into. `RefundPaymentCommand` goes in `Ecommerce.Contracts` because one service tells another; `PaymentTimeoutExpired` does not, because nobody else sends or hears it |
| **II. Clean Architecture Layering** | **Pass.** The orchestrator is the one service whose state machine and DbContext live in WebApi (CLAUDE.md); the sweeper sits beside them. In Payment the consumer is in WebApi and only dispatches `RefundOrderCommand` to the Application layer |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The sweeper stages its publishes and saves once, so the bus outbox carries them; the state machine's publishes go through the orchestrator's outbox (since `20260921104437_AddTransactionalOutbox`); a repeated timeout is `Ignore`d in `PaymentTimedOut` and discarded with no instance; the refund is once-only by the unique `refunds.OrderId` |
| **IV. Identity Comes From the Token** | **Pass, not engaged.** No endpoint was added; the saga works from message ids |
| **V. Evidence Over Assumption** | **Pass.** The four new transitions' tests failed before the code; the query runs against a real PostgreSQL; the end-to-end run stopped Payment past a 30-second timeout and restarted it; five mutations were each caught. Two mistakes in the author's own checks were found and recorded (see [research.md](research.md)) |

**Post-design re-check**: no violations. The rollback limit is not a violation of the constitution's schema
rule - no column changed - but it is the same kind of risk, and it is recorded in the roadmap.

## Project Structure

### Documentation (this feature)

```text
specs/053-saga-payment-timeout/
├── spec.md
├── plan.md             # This file
├── research.md         # D1-D6
├── data-model.md       # The new state value; the rows the sweeper reads
├── quickstart.md       # Tests, startup check, the end-to-end run
├── contracts/
│   └── messages.md     # RefundPaymentCommand; PaymentTimeoutExpired (internal)
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/BuildingBlocks/Ecommerce.Contracts/Payment/RefundPaymentCommand.cs` (new)
- Orchestrator:
  - `StateMachines/OrderStateMachine.cs`
  - `Timeouts/` (new): `PaymentTimeoutExpired`, `PaymentTimeoutOptions`, `PaymentTimeouts`,
    `PaymentTimeoutSweeper`
  - `Program.cs`
- Payment:
  - `RefundOrderCommand` + handler (reason)
  - `Consumers/RefundLatePaymentConsumer.cs`
  - `Program.cs` (registers the consumer)
- Tests:
  - `server/tests/Ecommerce.Orchestrator.Tests/` (new): `OrderStateMachineTests.cs`, `PaymentTimeoutTests.cs`
  - `Ecommerce.Payment.Tests` (`RefundTests.cs`)
  - `Ecommerce.slnx`
  - CI: `postgres-orchestrator` on 5436 in the build job
- `server/docker-compose.app.yml`: the orchestrator reads the inventory TTL too, for the startup check
- `server/.env.example`: `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS=600`
- Docs:
  - `docs/architecture/saga-orchestration-roadmap.md`
  - `docs/features/shopping-and-checkout.md`
  - `docs/testing/testing-strategy.md`
  - `docs/overview/project-overview.md`
  - `docs/project/*`
  - CLAUDE.md
  - regenerate `docs/reference`

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- The reservation step has no timeout of its own (out of scope in the spec).
- A refunded late payment is recorded, not shown to the customer.
- A rollback to an orchestrator older than this cannot load an instance in `PaymentTimedOut`; a late answer for
  such an order faults into the error queue.
- The sweep query has no index of its own on `(CurrentState, UpdatedAt)`; none was added, and no measurement of
  the query is recorded.
