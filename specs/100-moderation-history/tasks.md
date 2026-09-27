---
description: "Task list for Staff see a person's moderation history"
---

# Tasks: Staff see a person's moderation history

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included. They were written with the query, because they could not compile before it existed.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Entries name their person (US3)

- [X] T001 `AuditEntryRecorded.AboutUserId` (additive); `AuditTrail.RecordAsync(..., aboutUserId)` with the User-subject fallback
- [X] T002 [US3] Catalog: review, question and answer hide and restore, and product decisions name the person; Identity: shop approve and reject name the applicant
- [X] T003 Publisher assertions in `ReviewTests`, `ProductQuestionTests`, `ProductReviewTests`, `ModerationTests`, `ShopApplicationTests`; `AuditTrailTests.An_entry_says_whom_it_is_about`

## Phase 2: Activity keeps and serves it (US1, US2)

- [X] T004 `AuditEntry.AboutUserId`, index, migration `AddAuditAboutUser` with the User-subject backfill; the insert writes it
- [X] T005 `GetPersonModerationHistoryQuery` (Moderation fixed, reason from `After`, no snapshots), `GetAboutAsync`, `PersonHistoryController` (Staff)
- [X] T006 `PersonHistoryTests` (3)

## Phase 3: Storefront (US1, US2)

- [X] T007 `Accounts.history`, `usePersonHistory`, `queryKeys.personHistory`; decisions refresh the history
- [X] T008 [US1] `PersonHistory` in the stop dialog (latest 5, "and N earlier")
- [X] T009 [US2] "History…" menu item and `HistoryDialog` (paged); words en and vi, and four action labels
- [X] T010 Tests: the dialog's history, nothing on record, the paged menu history (3)

## Phase 4: Verification and docs

- [X] T011 Mutations (quickstart Scenario 5), each red; Activity 40, client 500
- [X] T012 [P] Bruno `admin-users/` seq 15, `security-checks/` seq 63
- [X] T013 Rebuilt Activity, Identity, Catalog and the storefront; backfill counted (1,500 of 1,520); Bruno 298/298
- [X] T014 Docs: `docs/features/moderation-and-staff.md`, `docs/features/audit-and-notifications.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T015 Merged as #207, closing #198
