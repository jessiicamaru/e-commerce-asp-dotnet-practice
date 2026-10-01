---
description: "Task list for Order lines fit any width"
---

# Tasks: Order lines fit any width

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Lines (US1)

- [ ] T001 `OrderLines` as two-column rows; long words break
- [ ] T002 Vitest: quantity × price, total, discount, seller per line
- [ ] T003 Flows: the checkout line total stays inside its card

## Phase 2: Totals (US2)

- [ ] T004 `OrderTotals` total row as one subgrid row; block at the right
- [ ] T005 Vitest: the total row holds label and amount together

## Phase 3: Verification and docs

- [ ] T006 Mutations, each red
- [ ] T007 Screens re-captured at checkout and 390px
- [ ] T008 Timeline, backlog
- [ ] T009 Merged, closes #239

## Evidence

(Filled in when the work is verified.)
