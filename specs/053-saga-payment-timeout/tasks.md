---
description: "Task list for Saga payment timeout"
---

# Tasks: Saga payment timeout

> Completed on 2026-09-27, after the feature merged (#135), from the code at that merge, the pull request and
> docs/architecture/saga-orchestration-roadmap.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included - the saga's first test project; the four new transitions' tests failed before the code
existed, and the four tests of existing behaviour passed before and after.

## Format: `[ID] [P?] [Story] Description`

- [X] T001 `RefundPaymentCommand` in `server/src/BuildingBlocks/Ecommerce.Contracts/Payment/RefundPaymentCommand.cs`
- [X] T002 [US3] `server/tests/Ecommerce.Orchestrator.Tests/` project, added to `Ecommerce.slnx`; state machine tests first (happy path, compensation, timeout, late approval, late rejection, timeout after completion, duplicate timeout)
- [X] T003 [US1] [US2] `PaymentTimedOut` state and transitions in `OrderStateMachine.cs`; `Timeouts/PaymentTimeoutExpired.cs`
- [X] T004 [US1] `Timeouts/PaymentTimeoutOptions.cs` (validated, TTL cross-check), `Timeouts/PaymentTimeouts.cs` (the due query), `Timeouts/PaymentTimeoutSweeper.cs`; `Program.cs`; sweeper + options tests against PostgreSQL on 5436
- [X] T005 [US2] Payment: `RefundOrderCommand` reason; `Consumers/RefundLatePaymentConsumer.cs`; tests in `server/tests/Ecommerce.Payment.Tests`
- [X] T006 CI `postgres-orchestrator` service in `.github/workflows/ci.yml`; compose passes the TTL to the orchestrator in `server/docker-compose.app.yml`
- [X] T007 End to end: Payment stopped past a short timeout, order fails and stock returns; Payment restarted, one refund recorded, order still Failed
- [X] T008 Mutation checks; docs `docs/architecture/saga-orchestration-roadmap.md`, `docs/features/shopping-and-checkout.md`, `docs/testing/testing-strategy.md`, `docs/project/*`, CLAUDE.md, `docs/reference` regenerated
- [X] T009 [P] [US3] `The_state_the_sweeper_looks_for_is_the_one_the_saga_stores` in `server/tests/Ecommerce.Orchestrator.Tests/OrderStateMachineTests.cs`, holding `PaymentTimeouts.AwaitingPayment` to the stored state name
- [X] T010 [P] `ORCHESTRATOR_PAYMENT_TIMEOUT_SECONDS=600` in `server/.env.example`; register `RefundLatePaymentConsumer` in `server/src/Services/Payment/Ecommerce.Payment.WebApi/Program.cs`
- [X] T011 [P] `docs/overview/project-overview.md` and decision in `docs/project/decisions.md`
- [X] T012 Merged as #135 (2026-09-24), closing #123

## Verification recorded in #135

- `Ecommerce.Orchestrator.Tests` 15/15; `Ecommerce.Payment.Tests` 20/20.
- End to end with a 30-second timeout: stock held while waiting (`onHand=24 reserved=1`); after about 55 s the
  order `Failed` and `reserved=0`; Payment restarted approved ₫20,416,000 and exactly one full refund was
  recorded; order still `Failed`, no stock moved, the cart kept its line. An earlier run that restarted Payment
  before the timeout completed normally (25 → 24).
- Mutations, each restored: no `Ignore` for a repeated timeout - 1 red; the timeout does not release the stock -
  3 red; a late approval also completes the order - 1 red; the query ignores the cutoff - 1 red; no hold check -
  1 red.

## Notes

T009-T012 were added on 2026-09-27 from the pull request; the work was part of #135 but had no task line.
