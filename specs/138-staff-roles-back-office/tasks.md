---
description: "Task list for Staff roles only in a back-office session"
---

# Tasks: Staff roles only in a back-office session

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Identity (US1, US2)

- [x] T001 `RefreshToken.Client`, its configuration, migration `AddSessionClient`
- [x] T002 `ISessionClient` (from `Origin`, `BackOffice:Origins`), `SessionClients.FromOrigin`
- [x] T003 `SessionRoles.Of` with the client; the token generator; login, the code, refresh, registrations
- [x] T004 `AuthResponse.StaffAccount`
- [x] T005 `BackOfficeSessionTests` against PostgreSQL; the fixture's client

## Phase 2: Storefront and tools (US3, US4)

- [x] T006 The storefront draws "Management platform" from `staffAccount`; Vitest
- [x] T007 `Origin` on the staff sign-in of the verify scripts, Bruno, the seed scripts and Playwright

## Phase 3: Verification and docs

- [x] T008 Mutations, each red; Bruno; the verify scripts; Playwright
- [x] T009 Docs: auth pages, moderation and staff, back office, ADR-003 progress, CLAUDE.md, timeline, backlog
- [x] T010 Merged, closes #278 - #284

## Evidence

- **Against the running stack** (Identity rebuilt): the administrator signed in with password and code twice, each time in a fresh 30-second window.
  - With `Origin: http://localhost:8088` the session holds `roles: []`. This account has no customer role, so on the storefront it holds nothing.
  - With `Origin: http://portal.localhost:8089` it holds `roles: ["Admin"]`.
  - `staffAccount` was true both times.
- **Identity, against PostgreSQL**: 272/272. `BackOfficeSessionTests` (15) covers:
  - storefront versus back office after the code;
  - a refresh keeping the app, even when asked from the other app;
  - a session from before reading as the storefront;
  - a customer is not staff;
  - the `Origin` rule: case, trailing slash, default ports, scheme, port, `null`, empty, and no origins configured.
- **Server mutations**, each red:
  - S1: roles ignore the app.
  - S2: refresh forgets the app.
  - S3: the code ignores `Origin`.
  - S4: an origin compared without its port.
  - S5: an old session read as the back office.
  - S6: no `staffAccount` after the code.
- **Client**: Vitest 722. Mutations caught: C1 (the back office offered from the session's roles), C2 (the account page ignores `hasBackOffice`).
- **Tools**:
  - `verify-auth.sh` and `verify-saga.sh` pass, signing in as the back office.
  - Bruno: 403/403, with `login admin - the code` asserting `staffAccount`.
  - Playwright: 7/7.
- **Docs**:
  - TOTP page §7: the role table by session, and why `Origin`.
  - Moderation and staff; back office; ADR-003 progress.
  - CLAUDE.md; the reference regenerated (`refresh_tokens.Client`).
