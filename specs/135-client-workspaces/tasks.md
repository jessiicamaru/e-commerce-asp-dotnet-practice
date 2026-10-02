---
description: "Task list for The client becomes a workspace of apps and packages"
---

# Tasks: The client becomes a workspace of apps and packages

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the existing 693, unchanged in substance, plus the layering test.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Layout (US1, US2)

- [x] T001 npm workspaces; move the app to `apps/storefront`, the kit to `packages/ui`, the core to `packages/core`
- [x] T002 Rewrite imports to package names; `cn` from the kit; move the three app-reaching tests into the app
- [x] T003 Shared aliases (`tsconfig.base.json`, Vite helper), Vitest projects, oxlint, Tailwind `@source`
- [x] T004 `shadcn` `components.json` in `packages/ui`
- [x] T005 The layering test

## Phase 2: Delivery

- [x] T006 Dockerfile, CI's `client` and `browser-e2e` jobs, Playwright
- [x] T007 Lint, 693 tests, build, the browser flows, the image check; a look in a browser
- [x] T008 ADR-003, client README, `docs/architecture/storefront.md`, glossary, CLAUDE.md, timeline, backlog
- [x] T009 Merged, closes #275 - #281

## Evidence

- **Same behaviour**:
  - `npm ci`, lint, `tsc -b` and the build all pass.
  - Vitest: 695 tests across 120 files. These are the 693 tests from before the move, plus 2 layering tests, run as two projects (storefront and core).
- **Layering test** (`packages/core/src/test/layering.test.ts`), with mutations, each red:
  - an `@/pages/cart` import added to a core service;
  - an `@ecommerce/core` import added to `packages/ui/src/button.tsx`.
- **Tailwind `@source`**: removing it shrank the built stylesheet from 103,499 to 53,875 bytes, and the build still passed. Restored, the size is back to 103,499 bytes, the same as the last build before the move (103,405) apart from the payment card added since.
- **shadcn**: `npx shadcn add switch` in `packages/ui` wrote `src/switch.tsx` importing `cn` from "cn", with no edits needed. The file was then removed.
  - It first failed with "Could not resolve the following aliases". The CLI matches an alias against a tsconfig key literally, so the kit's tsconfig now has an exact `@ecommerce/ui` key.
- **Image**: `verify-storefront-image.sh` passes for an image built from the workspace.
- **Browser**: Playwright 5/5 against the rebuilt storefront container. `npm run dev` serves the storefront from `client/`, styled (screenshot checked).
- **Docs**:
  - 142 doc links into `client/` repointed, and every one resolves.
  - New ADR-003.
  - Updated: client README, `architecture/storefront.md`, glossary, decision log #69, docs index, testing strategy, running-in-containers, CLAUDE.md.
