---
description: "Task list for The admin console's menu is grouped and shows what is waiting"
---

# Tasks: The admin console's menu is grouped and shows what is waiting

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Groups and counts (US1, US2)

- [x] T001 Grouped links, headings only with links
- [x] T002 `useStaffWaiting` by role; badges
- [x] T003 Vitest

## Phase 2: Where an order came from (US3)

- [x] T004 `state.from` on order links; breadcrumb and highlight
- [x] T005 Vitest

## Phase 3: Verification and docs

- [x] T006 Mutations, each red
- [x] T007 Checked in a browser
- [x] T008 Timeline, backlog
- [x] T009 Merged, closes #246 - #269

## Evidence

**2026-10-01**

- **Vitest**: an administrator sees six groups and the five counts (none for an empty queue); a moderator sees only
  Moderation and only their counts are asked for; an order opened from search keeps Find an order lit, opened directly
  Orders to ship; the order page's breadcrumb returns to the search with its filters, or to the queue.
- **Mutations, each red**: badges not drawn; a moderator asking for the administrator's queues; the opening list
  ignored; a fixed breadcrumb; no groups.
- **Browser** as the administrator: grouped menu, "Orders to ship 5"; an order opened from Find an order keeps that
  link lit and its breadcrumb returns to the search.
- **Found**: the moderation dashboard caches its one-row count pages under the lists' keys - filed as #268.
- **Client**: oxlint clean, type-check clean, Vitest 661/661, build green.
- **Post-design Constitution re-check**: unchanged.
