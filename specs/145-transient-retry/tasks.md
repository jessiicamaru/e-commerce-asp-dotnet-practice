---
description: "Task list for A consumer survives a transient database failure"
---

# Tasks: A consumer survives a transient database failure

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [ ] T001 `Ecommerce.Shared/Messaging/TransientRetry.cs`: `IsTransient`, `UseTransientRetry`
- [ ] T002 Every service's endpoint callback calls it before the outbox; Identity and Cart gain a callback
- [ ] T003 Tests: a `40001` consumer retried to success; a non-transient one faults at once; `IsTransient` cases
- [ ] T004 Mutation: the retry removed, and every exception treated as transient
- [ ] T005 The checkout load run with empty error queues
- [ ] T006 Docs: CLAUDE.md, reliable-messaging page, timeline, backlog
- [ ] T007 Merged, closes #299

## Evidence

(Filled in when the work is verified.)
