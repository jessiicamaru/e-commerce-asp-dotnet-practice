# Implementation Plan: MassTransit back on 8.3.6 after a broker outage stopped consumers for good

**Branch**: `fix/353-pin-masstransit` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md) | **Issue**: #353

## Summary

Every `MassTransit*` reference goes from 8.5.11 back to 8.3.6, Dependabot ignores MassTransit minors, and
`MassTransitVersionTests` holds the pin. Evidence: the whole test suite on 8.3.6, a mutation of the guard, and
`fault.sh broker` three or more times on the rebuilt stack.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: MassTransit, MassTransit.Abstractions, MassTransit.EntityFrameworkCore, MassTransit.RabbitMQ
8.3.6 (37 references in 28 projects)
**Storage**: none
**Testing**: `MassTransitVersionTests` (Inventory.Tests, beside `BrokerReconnectTests`); every suite; `fault.sh broker` x3+
**Constraints**: only MassTransit moves; the other updates of #326 stay
**Scale/Scope**: csproj versions, `dependabot.yml`, one test class, the record, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service keeps its own consumers and queues; only the library version changes. |
| **II. Clean Architecture Layering** | **Pass.** No layer changes. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The outbox, inbox and retries are MassTransit's on the version they were built and tested on (specs/145, 149, 154 were measured on 8.3.6). Nothing was lost on 8.5.11 either - the defect is availability, which this restores. |
| **IV. Identity Comes From the Token** | **Not applicable.** |
| **V. Evidence Over Assumption** | **Pass.** The decision rests on 3 of 3 failing runs on 8.5.11 against 0 of 7 on 8.3.6, with specs/154 excluded by a run without it; the fix is judged by new `fault.sh broker` runs, not by CI, which cannot see the defect. The root cause is stated as unknown rather than guessed. |

The constitution's stack line already says MassTransit 8.3.6; this makes it true again.

**Post-design re-check**: see `tasks.md`.

## Project Structure

```text
specs/155-pin-masstransit/                                      the record
server/**/*.csproj                                              MassTransit* 8.5.11 -> 8.3.6
.github/dependabot.yml                                          MassTransit minors ignored, with the reason
server/tests/Ecommerce.Inventory.Tests/MassTransitVersionTests.cs  the pin, in every csproj and in what the build resolved
docs/                                                           reliable messaging, evaluation, resilience results, timeline, backlog
CLAUDE.md                                                       the pin and why
```

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| None | - | - |
