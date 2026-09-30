---
description: "Task list for Administrators are told when an email fails for good"
---

# Tasks: Administrators are told when an email fails for good

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1)

- [x] T001 `outgoing_emails.FailureAlertedAt` + migration; the retry clears it
- [x] T002 The claim (one guarded statement) and the alert step in the dispatch, staging `EmailsFailed` to each administrator
- [x] T003 `EmailsFailed` in `NotificationKind` and `notification-kinds.json` (+ the `failed` placeholder)
- [x] T004 `EmailFailureAlertTests`: one notice per administrator; the hour; counted once, also by two sweeps at once; retried counts again; moderators not told

## Phase 2: Storefront (US1, US2)

- [x] T005 Words for `EmailsFailed` in both languages; `describeNotification` fills `failed`; the Overview's failed count; tests

## Phase 3: Verification and docs

- [x] T006 Mutations (quickstart Scenario 3), each red; every server suite and the client suite
- [x] T007 Rebuilt Identity container; the post-design Constitution re-check
- [x] T008 Docs: email page, audit-and-notifications page, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T009 Merged as #235, closes #222

## Evidence (2026-10-01)

- **Server**: `EmailFailureAlertTests` (4) against PostgreSQL - a failure tells each administrator once with
  `failed: 1` and the link, and no moderator; a failure within the hour tells nobody and is counted by the next notice
  (`failed: 2`); a retried email that fails again is counted again; two sweeps at once count six failures exactly once
  between them. The tests run on their own hours of a clock a year ahead and first mark every earlier failure counted,
  because the dispatcher sweeps a table other tests share.
- **Client**: 606/606 - the notice in both languages, every declared kind worded (the shared contract test now covers
  `EmailsFailed` and `failed`), the Overview's count from the email log linking to it.
- **Mutations, 7, every one red**: the quiet hour ignored, counted failures counted again, moderators told too, the
  retry keeping the mark, nobody told, the count wrong; in the storefront the count not filled in.
- **Live, in the rebuilt Identity and Activity containers**: an email to nobody inserted into the queue was failed by
  the dispatcher, marked counted, and the administrator's `EmailsFailed` notice (`{"failed": "1"}`,
  `/admin/email-delivery`) arrived in Activity.
- **Not tested**: that the notices commit only with the mark through the outbox - the test harness sees a publish
  whether or not the transaction saves; the order (publish, then the one save) is the codebase's rule and was read.
- **Post-design Constitution re-check**: unchanged.
