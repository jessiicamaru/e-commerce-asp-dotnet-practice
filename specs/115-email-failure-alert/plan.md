# Implementation Plan: Administrators are told when an email fails for good

**Branch**: `115-email-failure-alert` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md) | **Issue**: #222

## Summary

The dispatcher, after each sweep and in its transaction, claims the failed emails no notice has counted - at most once
an hour - and sends each administrator one `EmailsFailed` notice with the count. A retry clears the mark. The Overview
shows the failed count from the email log.

## Technical Context

**Language/Version**: C# / .NET 10; TypeScript / React 19
**Primary Dependencies**: EF Core + Npgsql, MassTransit outbox (`INotifier`), MediatR
**Storage**: Identity's PostgreSQL; one column
**Testing**: xUnit against PostgreSQL (with the test harness for the notices), Vitest
**Target Platform**: the compose stack and CI
**Project Type**: microservice + web storefront
**Performance Goals**: one extra statement per sweep
**Constraints**: the mark and the notices commit together (Principle III)
**Scale/Scope**: one column, one statement, one notice kind, one Overview card

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity marks its own rows and tells Activity through the existing notice message. |
| **II. Clean Architecture Layering** | **Pass.** The rule in the dispatch handler, the guarded statement in the repository. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** Claim and notices are staged in the sweep's transaction; Activity stores a notice once per publisher id. |
| **IV. Identity Comes From the Token** | **Not applicable.** A background sweep; recipients are the Admin role's holders. |
| **V. Evidence Over Assumption** | **Planned.** Tests against PostgreSQL including two sweeps at once, mutations. Recorded in `tasks.md`. |

**Post-design re-check** (after implementation): unchanged - see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/115-email-failure-alert/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/Services/Identity/…Domain/Entities/OutgoingEmail.cs (FailureAlertedAt), …Infrastructure/Migrations
server/src/Services/Identity/…Application/Email/DispatchEmailsCommand.cs (the alert step)
server/src/Services/Identity/…Infrastructure/Email/OutgoingEmailRepository.cs (claim, retry clears)
server/src/BuildingBlocks/Ecommerce.Shared/Notifications/ (EmailsFailed, notification-kinds.json)
server/tests/Ecommerce.Identity.Tests/EmailFailureAlertTests.cs
client/src/locales/*/notifications.json, utils/notifications, pages/admin-overview
```

## Complexity Tracking

No violation to justify.
