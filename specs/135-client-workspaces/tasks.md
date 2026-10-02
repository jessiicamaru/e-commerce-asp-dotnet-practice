---
description: "Task list for The client becomes a workspace of apps and packages"
---

# Tasks: The client becomes a workspace of apps and packages

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the existing 693, unchanged in substance, plus the layering test.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Layout (US1, US2)

- [ ] T001 npm workspaces; move the app to `apps/storefront`, the kit to `packages/ui`, the core to `packages/core`
- [ ] T002 Rewrite imports to package names; `cn` from the kit; move the three app-reaching tests into the app
- [ ] T003 Shared aliases (`tsconfig.base.json`, Vite helper), Vitest projects, oxlint, Tailwind `@source`
- [ ] T004 `shadcn` `components.json` in `packages/ui`
- [ ] T005 The layering test

## Phase 2: Delivery

- [ ] T006 Dockerfile, CI's `client` and `browser-e2e` jobs, Playwright
- [ ] T007 Lint, 693 tests, build, the browser flows, the image check; a look in a browser
- [ ] T008 ADR-003, client README, `docs/architecture/storefront.md`, glossary, CLAUDE.md, timeline, backlog
- [ ] T009 Merged, closes #275

## Evidence

(Filled in when the work is verified.)
