---
description: "Task list for A separate token audience for the back office"
---

# Tasks: A separate token audience for the back office

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Shared and Identity (US1, US2)

- [ ] T001 `JwtSettings.BackOfficeAudience`; both audiences valid; staff roles removed outside the back office
- [ ] T002 Identity issues back-office sessions' tokens for `BackOfficeAudience`
- [ ] T003 `BackOfficeAudienceTests` through a real pipeline; Identity's issued audience

## Phase 2: Verification and docs

- [ ] T004 Mutations, each red; Bruno; the verify scripts; Playwright
- [ ] T005 Docs: auth pages, back office, ADR-003 progress, CLAUDE.md, timeline, backlog
- [ ] T006 Merged, closes #280

## Evidence

(Filled in when the work is verified.)
