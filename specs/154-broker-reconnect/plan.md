# Implementation Plan: Services reconnect to the broker within seconds of its return

**Branch**: `fix/304-broker-reconnect` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md) | **Issue**: #304

## Summary

`BrokerReconnect.ReconnectQuickly()` in `Ecommerce.Shared/Messaging` replaces the RabbitMQ host's receive-transport
retry policy with the same exception filters and a 1–5 s exponential interval. Every service calls it in
`UsingRabbitMq`. Evidence: tests on a real bus configuration, a mutation, and `fault.sh broker` three times against the
four baseline runs.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: MassTransit 8.3.6 and MassTransit.RabbitMQ 8.3.6 (Shared gains the transport reference every
service already has)
**Storage**: none
**Testing**:
- `BrokerReconnectTests` (Inventory.Tests, beside `TransientRetryTests`);
- `fault.sh broker` x3;
- the services' logs (`Retrying 00:00:0x`).
**Constraints**: same exceptions handled; never a crash if it cannot apply
**Scale/Scope**: one shared class, eight `Program.cs` lines, tests, the record, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service reconnects on its own. Nothing couples them. |
| **II. Clean Architecture Layering** | **Pass.** Transport configuration in the shared building block, called by each host. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** It changes only *when* a connection is retried. The outbox, the inbox and the transient retry of specs/145 and 149 are untouched. |
| **IV. Identity Comes From the Token** | **Not applicable.** |
| **V. Evidence Over Assumption** | **Pass.** The cause was read from MassTransit's own code (research D1), not guessed. The fix is judged against four recorded baseline runs. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/154-broker-reconnect/                                          the record
server/src/BuildingBlocks/Ecommerce.Shared/Messaging/BrokerReconnect.cs  ReconnectQuickly()
server/src/BuildingBlocks/Ecommerce.Shared/Ecommerce.Shared.csproj       MassTransit.RabbitMQ 8.3.6
server/src/Services/*/...WebApi/Program.cs                               cfg.ReconnectQuickly(), eight services
server/tests/Ecommerce.Inventory.Tests/BrokerReconnectTests.cs           applied, same filters
```

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| Sets a private field of MassTransit's RabbitMQ host configuration | The interval is hard-coded in its constructor with no public setter (research D1), and it decides how long the whole stack stays stalled after an outage | *Accept and document*: 51–99 s of stall after every broker restart. *Restart the bus from a watchdog*: far more machinery, and it stops in-flight consumes. *Upgrade to MassTransit 9*: a commercial licence (specs/153). The reflection is contained in one method, falls back to the default, and a test fails if it stops applying |
