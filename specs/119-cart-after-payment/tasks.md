---
description: "Task list for The cart empties on screen when the order is paid"
---

# Tasks: The cart empties on screen when the order is paid

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Fix (US1)

- [ ] T001 `useOrder` re-reads the cart when the order settles, and once more
- [ ] T002 Hook test: settling → Paid re-reads; already settled does not
- [ ] T003 Flows: the cart badge is gone after paying

## Phase 2: Verification and docs

- [ ] T004 Mutations, each red
- [ ] T005 Timeline, backlog
- [ ] T006 Merged, closes #242

## Evidence

(Filled in when the work is verified.)
