# Implementation Plan: A consumer survives a transient database failure

**Branch**: `fix/299-transient-retry` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #299

## Summary

`Ecommerce.Shared/Messaging/TransientRetry.cs`:
- `IsTransient(exception)`: walks the inner exceptions for PostgreSQL's `40001`/`40P01` or a driver-transient
  connection failure;
- `UseTransientRetry(cfg)`: a jittered exponential retry that handles only those.

Every service's endpoint callback calls it before its EF outbox. Identity and Cart gain a callback calling it.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: MassTransit 8.3 (`UseMessageRetry`); Npgsql, read by property name, so Shared takes no
dependency on it
**Storage**: none
**Testing**:
- Inventory.Tests: a test-harness consumer failing with a `40001`-shaped exception, through the shared policy;
- the load test's checkout run, with the error queues inspected.
**Target Platform**: every service
**Constraints**: a non-transient failure must still fault at once
**Scale/Scope**: one Shared file, eight `Program.cs` lines, tests, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service retries its own consumers; nothing is shared at run time. |
| **II. Clean Architecture Layering** | **Pass.** A host concern, configured in WebApi from a Shared building block. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, and what this restores.** Each attempt is one transaction that commits whole or not at all, and the outbox drops a failed attempt's messages. Without a retry, "atomic" meant a transient failure lost the message's effect. |
| **IV. Identity Comes From the Token** | **Not applicable.** |
| **V. Evidence Over Assumption** | **Planned.** Found by measurement (#290); proven by a test that fails without the policy, and by the same load run passing with empty error queues. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/145-transient-retry/   spec, plan, research, data-model, quickstart, contracts/, checklists/, tasks
server/src/BuildingBlocks/Ecommerce.Shared/Messaging/TransientRetry.cs
server/src/Services/*/Ecommerce.*.WebApi/Program.cs     cfg.UseTransientRetry() before the outbox
server/tests/Ecommerce.Inventory.Tests/TransientRetryTests.cs
```

## Complexity Tracking

No violation to justify.
