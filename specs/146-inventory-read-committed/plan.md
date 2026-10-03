# Implementation Plan: Checkouts of one product do not queue behind retries

**Branch**: `fix/301-inventory-read-committed` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #301

## Summary

One setting, `o.IsolationLevel = IsolationLevel.ReadCommitted` on Inventory's `AddEntityFrameworkOutbox`, then
measured with the load runs that found the problem.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: MassTransit 8.3 EF outbox
**Storage**: Inventory's PostgreSQL, unchanged
**Testing**:
- the Inventory suite, which already runs every handler at `ReadCommitted`;
- the race (three times) and checkout load runs;
- the error queues and Inventory's log afterwards.
**Constraints**: correctness first; #299's retry stays
**Scale/Scope**: one line of configuration, the record, the measurement, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Inventory's own configuration. |
| **II. Clean Architecture Layering** | **Pass.** Host configuration, in WebApi. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Each consume is still one transaction with its inbox row and outbox messages. The stock row is still locked `FOR UPDATE` and changed by guarded statements. `ReadCommitted` is the isolation under which all of Inventory's concurrency tests already prove there is no overselling. |
| **IV. Identity Comes From the Token** | **Not applicable.** |
| **V. Evidence Over Assumption** | **Planned.** The same load runs before and after, the race three times, the suite, and the error queues and the log afterwards. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/146-inventory-read-committed/                                   the record
server/src/Services/Inventory/Ecommerce.Inventory.WebApi/Program.cs   o.IsolationLevel = ReadCommitted
```

## Complexity Tracking

No violation to justify.
