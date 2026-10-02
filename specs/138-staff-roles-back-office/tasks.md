---
description: "Task list for Staff roles only in a back-office session"
---

# Tasks: Staff roles only in a back-office session

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Identity (US1, US2)

- [ ] T001 `RefreshToken.Client`, its configuration, migration `AddSessionClient`
- [ ] T002 `ISessionClient` (from `Origin`, `BackOffice:Origins`), `SessionClients.FromOrigin`
- [ ] T003 `SessionRoles.Of` with the client; the token generator; login, the code, refresh, registrations
- [ ] T004 `AuthResponse.StaffAccount`
- [ ] T005 `BackOfficeSessionTests` against PostgreSQL; the fixture's client

## Phase 2: Storefront and tools (US3, US4)

- [ ] T006 The storefront draws "Management platform" from `staffAccount`; Vitest
- [ ] T007 `Origin` on the staff sign-in of the verify scripts, Bruno, the seed scripts and Playwright

## Phase 3: Verification and docs

- [ ] T008 Mutations, each red; Bruno; the verify scripts; Playwright
- [ ] T009 Docs: auth pages, moderation and staff, back office, ADR-003 progress, CLAUDE.md, timeline, backlog
- [ ] T010 Merged, closes #278

## Evidence

(Filled in when the work is verified.)
