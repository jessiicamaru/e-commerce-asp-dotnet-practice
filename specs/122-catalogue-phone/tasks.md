---
description: "Task list for The catalogue on a phone"
---

# Tasks: The catalogue on a phone

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Layout (US1)

- [x] T001 Two-column grid below `sm`; card price size
- [x] T002 Compact hero and scrolling chips below `sm`
- [x] T003 Price inputs on one row
- [x] T004 Image loading tint (+ Vitest)
- [x] T005 Browser test at 390px

## Phase 2: Verification and docs

- [x] T006 Mutation: the old grid, red
- [x] T007 Re-captured at 390px and 1440px
- [x] T008 Timeline, backlog
- [x] T009 Merged, closes #245 - #261

## Evidence

**2026-10-01**

- **Browser** (Edge, Vite dev, 390px): a new test - two products share the first row, the first starts at 807px (under
  1,100), nothing scrolls sideways. The page is 2,626px tall, from 6,453px.
- **Mutations, each red**: the old one-column grid ("the second product shares the first row"); the old hero (the first
  product at 1,355px); the image's loading tint removed (Vitest). The threshold was first 1,700px, which the old hero
  passed - measured both and set it between them.
- **Re-captured** at 390px (two to a row, Min – Max on one row, scrolling chips) and 1440px (unchanged).
- **Client**: oxlint clean, type-check clean, Vitest 623/623, build green.
- **Post-design Constitution re-check**: unchanged.
