---
description: "Task list for The cart empties on screen when the order is paid"
---

# Tasks: The cart empties on screen when the order is paid

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Fix (US1)

- [x] T001 `useOrder` re-reads the cart when the order settles, and once more
- [x] T002 Hook test: settling → Paid re-reads; already settled does not
- [x] T003 Flows: the cart badge is gone after paying

## Phase 2: Verification and docs

- [x] T004 Mutations, each red
- [x] T005 Timeline, backlog
- [x] T006 Merged, closes #242 - #258

## Evidence

**2026-10-01**

- **Hook (Vitest)**: an order seen `Submitted` then `Paid` re-reads the cart once at once and once after 2 s; an order
  already `Paid` when the page opened re-reads nothing.
- **Browser**: the Playwright flows assert no `Cart (n)` link in the header once the order is paid - 4/4 green against
  the stack (Edge, Vite dev).
- **Mutations, each red**: no re-read on settling (Vitest, and the browser: the header still showed `Cart (1)`); no
  second read (Vitest).
- **Client**: oxlint clean, type-check clean, Vitest 612/612, build green.
- **Post-design Constitution re-check**: unchanged.
