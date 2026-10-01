---
description: "Task list for The seller sidebar keeps to its column"
---

# Tasks: The seller sidebar keeps to its column

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Fix (US1)

- [x] T001 Sidebar and shop card columns may shrink; the full name as a tooltip
- [x] T002 The flows' seller has a long shop name; an assertion that the sidebar stays left of the page

## Phase 2: Verification and docs

- [x] T003 Mutation: the fix removed, the assertion red
- [x] T004 Client README layout note; timeline
- [x] T005 Merged, closes #238 - #256

## Evidence

**2026-10-01**

- **Browser**: the Playwright flows (Edge, Vite dev on :5173 against the compose stack) with the seller's shop named
  "E2e Lens House for Mirrorless and Film Cameras <run>": 4/4 green; the sale page's sidebar ends left of the page.
- **Mutation**: both `grid-cols-[minmax(0,1fr)]` removed - the assertion fails, the sidebar's right edge at 666px
  against the page's left at 344px. Restored.
- **Client**: oxlint clean, Vitest 606/606 (one earlier run had a single failure that did not recur on two reruns),
  build green.
- **Post-design Constitution re-check**: unchanged.
