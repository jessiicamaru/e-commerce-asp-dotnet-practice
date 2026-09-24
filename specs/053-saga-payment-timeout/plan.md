# Implementation Plan: Saga payment timeout

**Branch**: `053-saga-payment-timeout` | **Spec**: [spec.md](spec.md) | **Issue**: #123

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

**Sweeper (`PaymentTimeoutSweeper`)**
- It is shaped like Inventory's `ReservationExpirySweeper`: a `PeriodicTimer`, one scope per tick, and
  one bad tick never ends it.
- Each tick asks `PaymentTimeouts.DueAsync(context, cutoff)` for the ids of instances in
  `InventoryReservedState` whose `UpdatedAt` is older than the cutoff. It publishes one
  `PaymentTimeoutExpired` for each, then calls `SaveChangesAsync`, so the bus outbox carries them.
- `UpdatedAt` is set when the saga enters `InventoryReservedState`, which is the moment Inventory's hold
  also starts counting.

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

## Constitution check

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

## Files

- `server/src/BuildingBlocks/Ecommerce.Contracts/Payment/RefundPaymentCommand.cs` (new)
- Orchestrator:
  - `StateMachines/OrderStateMachine.cs`
  - `Timeouts/` (new): `PaymentTimeoutExpired`, `PaymentTimeoutOptions`, `PaymentTimeouts`,
    `PaymentTimeoutSweeper`
  - `Program.cs`
- Payment:
  - `RefundOrderCommand` + handler (reason)
  - `Consumers/RefundLatePaymentConsumer.cs`
- Tests:
  - `server/tests/Ecommerce.Orchestrator.Tests/` (new)
  - `Ecommerce.Payment.Tests`
  - `Ecommerce.slnx`
  - CI: `postgres-orchestrator` on 5436 in the build job
- `server/docker-compose.app.yml`: the orchestrator reads the inventory TTL too, for the startup check
- Docs:
  - `docs/architecture/saga-orchestration-roadmap.md`
  - `docs/features/shopping-and-checkout.md`
  - `docs/testing/testing-strategy.md`
  - `docs/project/*`
  - CLAUDE.md
  - regenerate `docs/reference`
