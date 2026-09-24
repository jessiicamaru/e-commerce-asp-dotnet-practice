# Tasks: Saga payment timeout

- [X] T001 `RefundPaymentCommand` in `server/src/BuildingBlocks/Ecommerce.Contracts/Payment/RefundPaymentCommand.cs`
- [X] T002 [US3] `server/tests/Ecommerce.Orchestrator.Tests/` project, added to `Ecommerce.slnx`; state machine tests first (happy path, compensation, timeout, late approval, late rejection, timeout after completion, duplicate timeout)
- [X] T003 [US1] [US2] `PaymentTimedOut` state and transitions in `OrderStateMachine.cs`; `Timeouts/PaymentTimeoutExpired.cs`
- [X] T004 [US1] `Timeouts/PaymentTimeoutOptions.cs` (validated, TTL cross-check), `Timeouts/PaymentTimeouts.cs` (the due query), `Timeouts/PaymentTimeoutSweeper.cs`; `Program.cs`; sweeper + options tests against PostgreSQL on 5436
- [X] T005 [US2] Payment: `RefundOrderCommand` reason; `Consumers/RefundLatePaymentConsumer.cs`; tests in `server/tests/Ecommerce.Payment.Tests`
- [X] T006 CI `postgres-orchestrator` service in `.github/workflows/ci.yml`; compose passes the TTL to the orchestrator in `server/docker-compose.app.yml`
- [X] T007 End to end: Payment stopped past a short timeout, order fails and stock returns; Payment restarted, one refund recorded, order still Failed
- [X] T008 Mutation checks; docs `docs/architecture/saga-orchestration-roadmap.md`, `docs/features/shopping-and-checkout.md`, `docs/testing/testing-strategy.md`, `docs/project/*`, CLAUDE.md, `docs/reference` regenerated
