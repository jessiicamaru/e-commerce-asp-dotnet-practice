---
description: "Task list for Old notices and audit entries are removed on a schedule"
---

# Tasks: Old notices and audit entries are removed on a schedule

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [ ] T001 `RetentionOptions` with its range checks; Activity refuses to start out of range
- [ ] T002 `ApplyRetentionCommand`: read notices, and audit entries when configured with the `AuditTrimmed` entry; batched deletes in the repository
- [ ] T003 `RetentionSweeper` (hosted, per interval) and the partial index migration
- [ ] T004 `RetentionTests`: read old gone, recent and unread stay; audit kept for ever by default; trimmed with an entry when set, none when nothing went; batches; two sweeps at once; settings

## Phase 2: Verification and docs

- [ ] T005 Mutations (quickstart Scenario 3), each red; every server suite
- [ ] T006 Rebuilt Activity container, Scenario 2; the post-design Constitution re-check
- [ ] T007 Docs: audit-and-notifications page (retention), CLAUDE.md, configuration guide, backlog, timeline; `generate_reference.py`
- [ ] T008 Merged as #236, closes #221
