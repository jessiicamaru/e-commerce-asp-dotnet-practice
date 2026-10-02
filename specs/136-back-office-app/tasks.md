---
description: "Task list for A back office for staff, with its own sign-in"
---

# Tasks: A back office for staff, with its own sign-in

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Shared pieces (US1)

- [ ] T001 `SignInForm` and the query-state messages to `packages/core/src/components`; the storefront's sign-in page uses the form
- [ ] T002 The theme to `packages/ui/src/theme.css`; both apps import it

## Phase 2: The app (US1-US3)

- [ ] T003 `apps/back-office`: Vite on `portal.localhost:5174`, providers, routes, sign-in, staff guard, greeting, sign-out
- [ ] T004 Translations (`backOffice` namespace) in both languages
- [ ] T005 Vitest: the guard (signed out, not staff, setup required, staff) and the form in the back office

## Phase 3: Shipping (US4)

- [ ] T006 Dockerfile `ARG APP`; compose `back-office` on `:8089`, `172.30.10.11`, trusted by the gateway
- [ ] T007 CI: build, check and scan the image, publish, prune; Playwright back-office flow
- [ ] T008 Mutations, each red; the image check; a look in a browser
- [ ] T009 Docs: ADR-003 progress, client README, architecture, running-in-containers, glossary, CLAUDE.md, timeline, backlog
- [ ] T010 Merged, closes #276

## Evidence

(Filled in when the work is verified.)
