---
description: "Task list for Product photographs fill their frame"
---

# Tasks: Product photographs fill their frame

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Frames (US1)

- [x] T001 Large images at their own ratio, capped; 4:3 while loading and for the tile
- [x] T002 Vitest per state
- [x] T003 Flows: upload a 3:2 photograph, measure it on the product page

## Phase 2: Verification and docs

- [x] T004 Mutation: the square frame back, red
- [x] T005 Timeline, backlog
- [x] T006 Merged, closes #251 - #264

## Evidence

**2026-10-01**

- **Browser**: the Playwright flows upload a 600×400 PNG (made in the test) before approval and measure the product
  page's photograph at 3:2 - 5/5 green (Edge, Vite dev).
- **Mutation**: the square frame back - the measurement fails, red.
- **Vitest**: large photograph 4:3 while loading, its own shape once loaded; cards and the large missing-photo tile
  keep 4:3. 636/636; lint, type-check, build green.
- **Post-design Constitution re-check**: unchanged.
