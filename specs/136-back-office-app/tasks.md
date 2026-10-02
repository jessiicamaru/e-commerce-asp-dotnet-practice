---
description: "Task list for A back office for staff, with its own sign-in"
---

# Tasks: A back office for staff, with its own sign-in

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Shared pieces (US1)

- [x] T001 `SignInForm` and the query-state messages to `packages/core/src/components`; the storefront's sign-in page uses the form
- [x] T002 The theme to `packages/ui/src/theme.css`; both apps import it

## Phase 2: The app (US1-US3)

- [x] T003 `apps/back-office`: Vite on `portal.localhost:5174`, providers, routes, sign-in, staff guard, greeting, sign-out
- [x] T004 Translations (`backOffice` namespace) in both languages
- [x] T005 Vitest: the guard (signed out, not staff, setup required, staff) and the form in the back office

## Phase 3: Shipping (US4)

- [x] T006 Dockerfile `ARG APP`; compose `back-office` on `:8089`, `172.30.10.11`, trusted by the gateway
- [x] T007 CI: build, check and scan the image, publish, prune; Playwright back-office flow
- [x] T008 Mutations, each red; the image check; a look in a browser
- [x] T009 Docs: ADR-003 progress, client README, architecture, running-in-containers, glossary, CLAUDE.md, timeline, backlog
- [x] T010 Merged, closes #276 - #282

## Evidence

- **Unit tests**:
  - Vitest: 704 across 122 files. These are the 695 from before, plus 9 in the new back-office project: 5 for the guard and 4 for sign-in.
  - The storefront's sign-in tests are unchanged and pass with the shared `SignInForm`.
  - Lint, `tsc -b` and both apps' builds pass.
- **Mutations**, each red:
  - M1: the guard lets anybody in.
  - M2: the guard does not send a signed-out visitor to sign in.
  - M3: setup-required is worded as not-staff.
  - M4: back-office sign-in forgets where the person was going.
  - M5: a dead challenge does not restart.
  - M6: the storefront loses its reason.
  - M7: the back office offers sign-up.
- **Separate sessions, in a browser**: the administrator signed in at `portal.localhost:5174`, then a customer signed in to the storefront in the same browser. After a reload the back office was still signed in as the administrator, and the refresh cookies were on `localhost` and `portal.localhost`.
- **Playwright**: 7/7 against compose, including `back-office.spec.ts` at `portal.localhost:8089`:
  - a moderator signs in with a code, stays signed in on a reload, and signs out (a reload then shows sign-in);
  - a customer is told the back office is for staff.
- **Images**:
  - `verify-storefront-image.sh ecommerce-back-office:local back-office` passes, and the image's title is "Back office · e-commerce".
  - The default build is still the storefront.
  - `APP=nope` stops the build with "no such app: nope".
  - In compose, the gateway runs with `GATEWAY_TRUSTED_PROXIES=172.30.10.10,172.30.10.11`.
- **Docs**: new `docs/architecture/back-office.md`; ADR-003 progress; client README; running-in-containers; testing strategy; docs index; CLAUDE.md.
