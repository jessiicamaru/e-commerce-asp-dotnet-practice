---
description: "Task list for A message delivered twice at once is consumed once and faults neither time"
---

# Tasks: A message delivered twice at once is consumed once and faults neither time

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first.

- [ ] T001 `InboxRedeliveryTests`: one message delivered twice at once through the real EF inbox; it fails before the fix with the production fault
- [ ] T002 `TransientRetry.IsTransient`: a `23505` on `AK_InboxState_MessageId_ConsumerId` is transient
- [ ] T003 `TransientRetryTests`: the inbox's `23505` counts, any other `23505` still does not
- [ ] T004 Mutations; the Inventory, Payment and Order suites
- [ ] T005 `fault.sh broker` on the rebuilt stack: every `_error` queue empty
- [ ] T006 Docs: reliable messaging, CLAUDE.md, timeline, backlog
- [ ] T007 Merged, closes #306

## Evidence

(Filled in when the work is verified.)
