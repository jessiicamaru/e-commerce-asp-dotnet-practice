---
description: "Task list for Order lines fit any width"
---

# Tasks: Order lines fit any width

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Lines (US1)

- [x] T001 `OrderLines` as two-column rows; long words break
- [x] T002 Vitest: quantity × price, total, discount, seller per line
- [x] T003 Flows: the checkout line total stays inside its card

## Phase 2: Totals (US2)

- [x] T004 `OrderTotals` total row as one subgrid row; block at the right
- [x] T005 Vitest: the total row holds label and amount together

## Phase 3: Verification and docs

- [x] T006 Mutations, each red
- [x] T007 Screens re-captured at checkout and 390px
- [x] T008 Timeline, backlog
- [x] T009 Merged, closes #239 - #257

## Evidence

**2026-10-01**

- **Browser**: the Playwright flows assert the checkout line total stays inside its card (the seller's long shop name
  in "Sold by" makes the line narrower still): 4/4 green against the stack (Edge, Vite dev).
- **Mutations, each red**: the old three-column table with only the test id added (browser: total's right edge 1205px
  against the card's 1200px); the total allowed to wrap; long words unbroken; the total's rule on two cells (Vitest).
- **Re-captured** at 1440px and 390px: checkout and the order page show every amount whole, the rule continuous, the
  totals under the line totals.
- **Client**: oxlint clean, Vitest 610/610, build green.
- **Post-design Constitution re-check**: unchanged.
