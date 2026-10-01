---
description: "Task list for Staff see a deleted account as deleted"
---

# Tasks: Staff see a deleted account as deleted

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1, US2)

- [ ] T001 `DeletedAt` on the response; `includeDeleted` through query, repository and controller
- [ ] T002 `TargetAsync` refuses a deleted account with 409 `AccountDeleted`
- [ ] T003 Identity tests: hidden by default, listed when asked with the date, each command refused

## Phase 2: Storefront

- [ ] T004 Badge, no actions, the box; service and hook pass the parameter
- [ ] T005 Vitest

## Phase 3: Verification and docs

- [ ] T006 Mutations, each red
- [ ] T007 Bruno request; rebuilt Identity; the page in a browser
- [ ] T008 Accounts feature page, generate_reference.py, timeline, backlog
- [ ] T009 Merged, closes #241

## Evidence

(Filled in when the work is verified.)
