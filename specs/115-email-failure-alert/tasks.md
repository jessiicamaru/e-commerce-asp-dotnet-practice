---
description: "Task list for Administrators are told when an email fails for good"
---

# Tasks: Administrators are told when an email fails for good

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1)

- [ ] T001 `outgoing_emails.FailureAlertedAt` + migration; the retry clears it
- [ ] T002 The claim (one guarded statement) and the alert step in the dispatch, staging `EmailsFailed` to each administrator
- [ ] T003 `EmailsFailed` in `NotificationKind` and `notification-kinds.json` (+ the `failed` placeholder)
- [ ] T004 `EmailFailureAlertTests`: one notice per administrator; the hour; counted once, also by two sweeps at once; retried counts again; moderators not told

## Phase 2: Storefront (US1, US2)

- [ ] T005 Words for `EmailsFailed` in both languages; `describeNotification` fills `failed`; the Overview's failed count; tests

## Phase 3: Verification and docs

- [ ] T006 Mutations (quickstart Scenario 3), each red; every server suite and the client suite
- [ ] T007 Rebuilt Identity container; the post-design Constitution re-check
- [ ] T008 Docs: email page, audit-and-notifications page, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T009 Merged as #235, closes #222
