---
description: "Task list for A one-time handoff from the storefront to the back office"
---

# Tasks: A one-time handoff from the storefront to the back office

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Identity and the gateway (US2)

- [ ] T001 `BackOfficeHandoff`, its configuration, migration `AddBackOfficeHandoffs`; personal-data inventory and erasure
- [ ] T002 Issue and redeem handlers, the guarded claim, the endpoints; audit
- [ ] T003 The gateway's redeem route under the sign-in limit
- [ ] T004 `HandoffTests` against PostgreSQL

## Phase 2: The apps (US1)

- [ ] T005 Storefront: "Management platform" issues a handoff and opens the callback
- [ ] T006 Back office: `/auth/callback`; the sign-in form can start at the code step
- [ ] T007 Vitest for both

## Phase 3: Verification and docs

- [ ] T008 Bruno; Playwright crossing from the storefront; mutations
- [ ] T009 Docs: TOTP page, back office page, ADR-003 progress, CLAUDE.md, reference, timeline, backlog
- [ ] T010 Merged, closes #279

## Evidence

(Filled in when the work is verified.)
