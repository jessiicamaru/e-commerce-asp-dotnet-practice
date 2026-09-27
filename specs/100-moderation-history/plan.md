# Implementation Plan: Staff see a person's moderation history

**Branch**: `100-moderation-history` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #198

## Summary

Audit entries gain the person they are about. `AuditEntryRecorded.AboutUserId` is additive. `AuditTrail` fills it from a
User subject, and the Catalog and Identity moderation decisions about content name its author or seller. Activity
stores the column, backfills it for User subjects, and serves `GET /api/audit/people/{userId}` to staff. That route
returns Moderation entries only, each with the reason and no snapshots. The users page shows the latest decisions in the
lock and ban dialog and the whole history from each person's menu.

## Technical Context

- **Contracts**: `Activity/AuditEntryRecorded.cs` (`AboutUserId`).
- **Shared**: `Audit/AuditTrail.cs` (`aboutUserId`, and the User-subject fallback).
- **Identity**: `ShopApplicationFeatures` (approve and reject name the applicant).
- **Catalog**: `ReviewFeatures`, `QuestionFeatures` and `ProductReviewFeatures`, whose hide, restore and decide calls
  name the person.
- **Activity**:
  - `AuditEntry.AboutUserId`, its configuration and index, and migration `AddAuditAboutUser` with the backfill.
  - `RecordAuditEntryCommand` and `AuditRepository` (insert, `GetAboutAsync`).
  - `Audit/Queries/PersonHistory.cs` (new) and `PersonHistoryController` (new).
- **Storefront**:
  - `Accounts.history`, `usePersonHistory` and `queryKeys.personHistory`; a decision also refreshes the history.
  - `pages/admin-users/person-history.tsx` and `history-dialog.tsx` (new), the stop dialog, and the menu item.
  - Words in en and vi, including four action labels the audit page lacked.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: EF Core 10 + Npgsql, MassTransit, System.Text.Json; TanStack Query

**Storage**: one column and one index in `ecommerce_activity_db` (5440)

**Testing**: xUnit against real PostgreSQL; Vitest; Bruno

**Target Platform**: Activity (5063), with Identity and Catalog as publishers, and the storefront

**Performance Goals**: one indexed range read per page of history

**Constraints**: a moderator must not reach any other part of the log; a rolling deploy must work in both directions

**Scale/Scope**: 1 field, 1 column, 1 route, 2 client components

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The publisher names the person from its own data, and Activity answers from its own table. No synchronous call is added; research D1 rejects the lookup precisely because it would add one. |
| **II. Clean Architecture Layering** | **Pass.** The rule (Moderation only, the reason only) sits in the Application query, the SQL in the repository, and the controller only sends. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The field travels in the same outbox message as before, staged in the same place. Activity's insert is still one `ON CONFLICT DO NOTHING` statement. |
| **IV. Identity Comes From the Token** | **Pass.** The route's permission is the Staff role. The person asked about is data, not identity, and the actor on each entry still comes from `ICurrentUser` at the publisher. |
| **V. Evidence Over Assumption** | **Pass.** 4 new Activity tests; publisher assertions in 5 existing Catalog and Identity tests; 3 client tests; six mutations each caught, plus a seventh test (Identity `ModerationTests`) that also caught the fallback's removal; the backfill counted on the development database; Bruno 298/298 through rebuilt containers. |

The migration is expand only: a nullable column plus a data backfill. A previous image ignores the column.

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/100-moderation-history/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D5
├── data-model.md        # The column, the backfill, who is named
├── quickstart.md
├── contracts/
│   ├── http-api.md      # GET /api/audit/people/{userId}
│   └── messages.md      # AuditEntryRecorded.AboutUserId
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

As in Technical Context. Docs: `docs/features/moderation-and-staff.md`, `docs/features/audit-and-notifications.md`,
CLAUDE.md, the backlog and the timeline, and a run of `generate_reference.py`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.
