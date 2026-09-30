# Implementation Plan: Old notices and audit entries are removed on a schedule

**Branch**: `116-activity-retention` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #221

## Summary

A sweeper in Activity deletes read notices older than a setting (90 days) and - only when an operator sets one - audit
entries older than a number of years, recording each trim of the audit log in the audit log. Short batches, safe on
several instances, settings refused at startup when out of range.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: EF Core + Npgsql, MassTransit outbox (`IAuditTrail`), MediatR
**Storage**: Activity's PostgreSQL; one partial index
**Testing**: xUnit against PostgreSQL
**Target Platform**: the compose stack and CI
**Project Type**: microservice
**Performance Goals**: each batch a short statement over an index
**Constraints**: never an unread notice; never the audit log unless configured, and never silently
**Scale/Scope**: one sweeper, one command, one options class, one index

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Activity deletes its own rows. |
| **II. Clean Architecture Layering** | **Pass.** The rule in an Application command, the batched deletes in the repository, the timer in Infrastructure. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Deleting is idempotent; the trim's audit entry is staged and saved with the batch that deleted audit entries. |
| **IV. Identity Comes From the Token** | **Not applicable.** A background sweep acting as the system. |
| **V. Evidence Over Assumption** | **Planned.** Tests against PostgreSQL including two sweeps at once, mutations, a run in the stack. Recorded in `tasks.md`. |

**Post-design re-check**: to be done once implemented; results go in `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/116-activity-retention/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Activity/Ecommerce.Activity.Application/Retention/ (options, ApplyRetentionCommand)
server/src/Services/Activity/Ecommerce.Activity.Infrastructure/ (RetentionRepository, RetentionSweeper, migration)
server/src/Services/Activity/Ecommerce.Activity.WebApi/Program.cs, appsettings.json
server/tests/Ecommerce.Activity.Tests/RetentionTests.cs
```

## Complexity Tracking

No violation to justify.
