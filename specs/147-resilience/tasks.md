---
description: "Task list for Checkout survives a service or the broker going down"
---

# Tasks: Checkout survives a service or the broker going down

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the scenarios assert themselves.

- [ ] T001 `resilience.js`: steady checkouts without waiting; the teardown settles every order (long timeout), records when, and checks the invariants
- [ ] T002 `fault.sh`: load, fault, recover; the timeline; the error queues after; the exit code
- [ ] T003 Run payment, broker, orchestrator and inventory; any defect found gets its own issue
- [ ] T004 The negative control
- [ ] T005 `resilience_report.py` and `docs/testing/resilience-results.md`; testing strategy, docs index, CLAUDE.md, timeline, backlog
- [ ] T006 Merged, closes #291

## Evidence

(Filled in when the work is verified.)
