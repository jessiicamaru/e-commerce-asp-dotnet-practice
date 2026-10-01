---
description: "Task list for Staff see a deleted account as deleted"
---

# Tasks: Staff see a deleted account as deleted

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [x] T001 `DeletedAt` on the response; `includeDeleted` through query, repository and controller
- [x] T002 `TargetAsync` refuses a deleted account with 409 `AccountDeleted`
- [x] T003 Identity tests: hidden by default, listed when asked with the date, each command refused

## Phase 2: Storefront

- [x] T004 Badge, no actions, the box; service and hook pass the parameter
- [x] T005 Vitest

## Phase 3: Verification and docs

- [x] T006 Mutations, each red
- [x] T007 Bruno request; rebuilt Identity; the page in a browser
- [x] T008 Accounts feature page, generate_reference.py, timeline, backlog
- [x] T009 Merged, closes #241 - #262

## Evidence

**2026-10-01**

- **Identity** (PostgreSQL): `DeletedAccountStaffTests` (7) - a deleted account is not listed by default and is listed
  with `deletedAt` when asked; a live account is listed either way with none; lock, unlock, ban, lift, grant and revoke
  on a deleted account are each 409 `AccountDeleted` and write nothing. All 254 Identity tests green.
- **Mutations, each red**: the repository's filter off (1 red); the refusal in `TargetAsync` off (6 red); on the page,
  the Deleted badge, the hidden actions and the box's parameter (each red).
- **Bruno** against the rebuilt Identity container: three requests in `my-data` after the deletion - not listed by
  default, listed with `deletedAt` when asked, lock is 409 `AccountDeleted`. 386/386 requests, 625 tests.
- **Client**: oxlint clean, type-check clean, Vitest 625/625, build green.
- **Docs**: moderation-and-staff (users console, API), CLAUDE.md, `generate_reference.py`.
- **Post-design Constitution re-check**: unchanged.
