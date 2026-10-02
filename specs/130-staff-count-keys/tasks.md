---
description: "Task list for Staff counts never share a list's cache"
---

# Tasks: Staff counts never share a list's cache

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Fix (US1)

- [x] T001 Pages read `useStaffWaiting`
- [x] T002 Page size in two keys
- [x] T003 Decisions invalidate `staff-waiting`
- [x] T004 Vitest: dashboard then queue with one cache; counts re-read after a decision

## Phase 2: Verification and docs

- [x] T005 Mutations, each red
- [x] T006 Timeline, backlog
- [x] T007 Merged, closes #268 - #270

## Evidence

**2026-10-02**

- **Vitest**: with one QueryClient, the moderation dashboard and then the products queue - the queue shows nothing
  until its own page arrives, where the cached one-row count page used to show; approving a product invalidates
  `staff-waiting`; the review-queue, shop-application and outgoing-email keys differ by page size under one prefix.
- **Mutations, each red**: the defect back (the old key and the list hook on the dashboard); the key without its size
  (caught by the key test - the page test alone let it through, since no reader shares the key now); no count refresh
  after a decision.
- **Client**: oxlint clean, type-check clean, Vitest 666/666, build green.
- **Post-design Constitution re-check**: unchanged.
