# Implementation Plan: A message delivered twice at once is consumed once and faults neither time

**Branch**: `fix/306-inbox-redelivery` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md) | **Issue**: #306

## Summary

`TransientRetry.IsTransient` (specs/145) learns one more case: a `23505` whose constraint is the inbox's own key. A
test drives the real EF outbox and inbox on Payment's PostgreSQL with one message delivered twice at once. It was
written before the fix and failed with the production fault.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: MassTransit 8.3 (EF outbox and inbox), Npgsql
**Storage**: unchanged. The inbox table and its key are MassTransit's.
**Testing**:
- `InboxRedeliveryTests` (Payment.Tests, real PostgreSQL, the in-memory harness with the EF outbox);
- `TransientRetryTests` extended for the predicate;
- `fault.sh broker`.
**Constraints**: a unique violation on the shop's own data must still fault
**Scale/Scope**: one predicate, two test classes, the record, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** A shared building block every service already calls. No service learns about another. |
| **II. Clean Architecture Layering** | **Pass.** Messaging configuration in `Ecommerce.Shared`, used by each host. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, and the point.** The inbox is the idempotency mechanism. This change lets it do its job: a duplicate is recognised on retry and dropped, rather than reported as a failure. Each retry is a new transaction wrapping the inbox and outbox, as in specs/145. |
| **IV. Identity Comes From the Token** | **Not applicable.** |
| **V. Evidence Over Assumption** | **Planned.** The race is reproduced against the real inbox before the fix, with the production fault message. The broker fault is run again after. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/149-inbox-redelivery/                                          the record
server/src/BuildingBlocks/Ecommerce.Shared/Messaging/TransientRetry.cs   IsTransient: the inbox's unique key
server/tests/Ecommerce.Payment.Tests/InboxRedeliveryTests.cs             the race on the real inbox
server/tests/Ecommerce.Inventory.Tests/TransientRetryTests.cs            the predicate
```

## Complexity Tracking

No violation to justify.
