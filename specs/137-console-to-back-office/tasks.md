---
description: "Task list for The admin and moderator console moves to the back office"
---

# Tasks: The admin and moderator console moves to the back office

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the console's own, moved, plus the new ones below.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Move (US1)

- [x] T001 The 36 shared components to `packages/core/src/components`
- [x] T002 The console's pages, layout and tests to `apps/back-office`; `/admin/x` to `/x`; routes and role guards
- [x] T003 The back office's frame around the console's layout; its home is the console's home

## Phase 2: Between the apps (US2, US3)

- [x] T004 `core/config/apps` and `/app-config.js` (nginx + `public/`); console links to products open the storefront
- [x] T005 Storefront: `/admin/*` redirects; header and account link to the back office; notice links mapped
- [x] T006 Closing a shop in the back office (approved applications, sellers in People); off the shop page
- [x] T007 Vitest for the redirect, the notice links, the app addresses and the close-shop placements

## Phase 3: Verification and docs

- [x] T008 Playwright: approval in the back office; mutations, each red; a look in a browser
- [x] T009 Docs: client README, back office page, ADR-003 progress, feature pages, CLAUDE.md, timeline, backlog
- [x] T010 Merged, closes #277 - #283

## Evidence

- **Analysis first.** An import graph of the storefront found no component used only by the console. 36 were used by both the console and the shop, and they depend only on each other, the core and the kit. They moved to `packages/core/src/components` as a closed set, together with `RequireRole`.
- **Unit tests**:
  - Vitest: 718 across 128 files. These are the console's own tests (moved), plus new ones: app addresses, the `/admin` mapping, notice links, `ProductLink` in each app, `ToBackOffice`, the user menu opening the back office, close-shop in both new places, the shop page offering staff nothing, and the back office's routes.
  - `tsc -b`, lint and both builds pass.
- **Mutations**, each red:
  - M1: `/admin` alone not mapped.
  - M2: notice links stay in the storefront.
  - M3: no redirect.
  - M4: `ProductLink` always in-app.
  - M5: account destinations always routed.
  - M6: an approved shop cannot be closed.
  - M7: close-shop offered to non-sellers.
  - M8: a moderator kept on the console's home. This one first survived, because nothing tested the back office's routes; `routes/index.test.tsx` was added and it went red.
- **A defect found by a test**: the user menu called `navigate(absoluteUrl)`, which react-router treats as a path. `followDestination` now does a full navigation.
- **Playwright**: 7/7 against compose. The moderator approves a product in the back office, and signing in lands on `/moderation`.
- **Browser**:
  - The administrator lands on Orders to ship with the console's grouped menu.
  - `localhost:8088/admin/users?role=Moderator` arrives at `portal.localhost:8089/users?role=Moderator` with the filter applied.
  - `/app-config.js` from the storefront container names both apps.
- **Image check**: it also asks for `/app-config.js`, and passes for the back office.
- **Docs**:
  - The back office page: state, code table, and the new "Between the two apps" section.
  - The storefront page: console rows replaced.
  - ADR-003 progress.
  - The docs index and the glossary: `/admin/x` in older pages is the back office's `/x`.
  - 27 doc links repointed to the moved files, and every one resolves.
  - CLAUDE.md.
