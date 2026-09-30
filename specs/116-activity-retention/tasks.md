---
description: "Task list for Old notices and audit entries are removed on a schedule"
---

# Tasks: Old notices and audit entries are removed on a schedule

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [x] T001 `RetentionOptions` with its range checks; Activity refuses to start out of range
- [x] T002 `ApplyRetentionCommand`: read notices, and audit entries when configured with the `AuditTrimmed` entry; batched deletes in the repository
- [x] T003 `RetentionSweeper` (hosted, per interval) and the partial index migration
- [x] T004 `RetentionTests`: read old gone, recent and unread stay; audit kept for ever by default; trimmed with an entry when set, none when nothing went; batches; two sweeps at once; settings

## Phase 2: Verification and docs

- [x] T005 Mutations (quickstart Scenario 3), each red; every server suite
- [x] T006 Rebuilt Activity container, Scenario 2; the post-design Constitution re-check
- [x] T007 Docs: audit-and-notifications page (retention and its settings - there is no separate configuration guide), CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T008 Merged as #236, closes #221

## Evidence (2026-10-01)

- **Server**: `RetentionTests` (9) against PostgreSQL - a notice read 91 days ago goes, one read 89 days ago and an unread
  one of 400 days stay; batches of 2 remove 5; two sweeps at once remove 6 and neither fails; with no years set an audit
  entry of 30 years stays; with 5 years, entries of 6-8 years go and one of 4 stays, the `AuditTrimmed` entries' counts
  add up to what went, and a sweep with nothing to remove records nothing; each setting out of range is named. Activity
  52/52; Identity 247, Catalog 261, Order 343 unaffected.
- **Mutations, 7, every one red**: unread notices deleted too, the cutoff ignored, the audit log trimmed with no
  setting, no trim entry, one batch only, a day of 0 accepted, 0 years accepted. (Removing `"ReadAt" IS NOT NULL` alone is
  an equivalent mutant - a null never compares below the cutoff - so the mutation deletes unread ones outright.)
- **Live, in the rebuilt Activity container**: a notice read 100 days ago was removed by the sweep at start, an unread
  one from 200 days ago stayed, the log said "Retention removed 1 read notice(s) and 0 audit entr(ies)", and
  `IX_notifications_ReadAt` exists.
- **Decided while building**: an `AuditTrimmed` entry per batch rather than per sweep, so each commits with the deletes
  it describes (spec, research D2 and the data model say so).
- **Post-design Constitution re-check**: unchanged.
