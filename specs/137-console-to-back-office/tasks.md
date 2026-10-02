---
description: "Task list for The admin and moderator console moves to the back office"
---

# Tasks: The admin and moderator console moves to the back office

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the console's own, moved, plus the new ones below.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Move (US1)

- [ ] T001 The 36 shared components to `packages/core/src/components`
- [ ] T002 The console's pages, layout and tests to `apps/back-office`; `/admin/x` to `/x`; routes and role guards
- [ ] T003 The back office's frame around the console's layout; its home is the console's home

## Phase 2: Between the apps (US2, US3)

- [ ] T004 `core/config/apps` and `/app-config.js` (nginx + `public/`); console links to products open the storefront
- [ ] T005 Storefront: `/admin/*` redirects; header and account link to the back office; notice links mapped
- [ ] T006 Closing a shop in the back office (approved applications, sellers in People); off the shop page
- [ ] T007 Vitest for the redirect, the notice links, the app addresses and the close-shop placements

## Phase 3: Verification and docs

- [ ] T008 Playwright: approval in the back office; mutations, each red; a look in a browser
- [ ] T009 Docs: client README, back office page, ADR-003 progress, feature pages, CLAUDE.md, timeline, backlog
- [ ] T010 Merged, closes #277

## Evidence

(Filled in when the work is verified.)
