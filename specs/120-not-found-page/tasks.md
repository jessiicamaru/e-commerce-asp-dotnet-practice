---
description: "Task list for A real not-found page"
---

# Tasks: A real not-found page

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Page (US1)

- [x] T001 `NotFoundPage` with heading, sentence, search and link; the route uses it
- [x] T002 Keys in both languages
- [x] T003 Tests: both languages, the search navigates, the link goes home

## Phase 2: Verification and docs

- [x] T004 Mutations, each red
- [x] T005 Timeline, backlog
- [x] T006 Merged, closes #243 - #259

## Evidence

**2026-10-01**

- **Vitest** (3): the page in English and in Vietnamese (no English left), the link home, and the search sending
  `/?q=canon%20r50` for "  canon r50 ".
- **Mutations, each red**: the search untrimmed; the link pointing elsewhere; the Vietnamese words removed.
- **Browser** (Edge, Vite dev, the real route table): `/no-such-page` in both languages at 1440px and 390px; the page's
  search lands on `/?q=canon`.
- **Client**: oxlint clean, type-check clean, Vitest 615/615, build green.
- **Post-design Constitution re-check**: unchanged.
