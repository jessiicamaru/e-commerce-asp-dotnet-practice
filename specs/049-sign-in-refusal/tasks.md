---
description: "Task list for Sign-in refusal"
---

# Tasks: Sign-in refusal

> Completed on 2026-09-27, after the feature merged (#130), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included - the page tests were written first and 6 of 8 failed on the old page.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US2)

- [X] T001 [US2] `ForbiddenException` carries optional facts in `server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/ForbiddenException.cs`; `GlobalExceptionHandler` writes them as extensions, reserved keys winning
- [X] T002 [US2] Handler test in `server/tests/Ecommerce.Identity.Tests/ForbiddenProblemTests.cs`: facts reach the 403 body in Production; `traceId` cannot be overwritten
- [X] T003 [US2] `LoginCommandHandler` refuses with `code`, `until`, `reason`; assert them in `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`
- [X] T004 [US2] Bruno `bruno/admin-users/the locked customer cannot sign in.yml` asserts `code`, `until`, `reason`

## Phase 2: Client (US1)

- [X] T005 [US1] Client tests first in `client/src/pages/sign-in/index.test.tsx`: locked, banned (both languages), wrong password, other 403, no body
- [X] T006 [US1] `client/src/pages/sign-in/refusal.ts` + `index.tsx` + `client/src/locales/{en,vi}/auth.json`
- [X] T008 [P] [US1] `client/src/config/axios/api-error.ts`: `code`, `until`, `reason` on `ProblemDetails`; `client/src/test/refusal.ts` builds a refusal with extensions for the tests

## Phase 3: Verification and docs

- [X] T007 Mutation checks, then docs: `docs/features/moderation-and-staff.md`, `docs/architecture/error-handling-and-shared-building-block.md`, `docs/project/timeline.md`, `docs/project/backlog.md`
- [X] T009 End to end against a rebuilt Identity container through the gateway: locked + right password → 403 with the three facts; locked + wrong password → the bare 401; banned → 403 with `code` and `reason`, no `until`
- [X] T010 `CLAUDE.md` records that `ForbiddenException` can carry facts, written in every environment
- [X] T011 Merged as #130 (2026-09-24), closing #120

## Verification recorded in #130

- Client: 6 of the 8 new page tests failed before the fix (the two that passed - wrong password, no body -
  the old page already handled); after it 8/8, the full suite 279/279, lint and build clean.
- Server: `Ecommerce.Identity.Tests` 76/76 against real PostgreSQL.
- Mutations, each restored: handler stops copying facts - 2 red; a fact may overwrite `traceId` - 1 red;
  login drops `until` - red; page back to "every non-401 is generic" - 5 red; page ignores `AccountLocked` -
  2 red; a 401 with a detail stops being "wrong" - 1 red.

## Notes

T008-T011 were added on 2026-09-27 from the pull request; the work they describe was part of #130 but had no
task line. T007's number is kept, so the numbering is not in phase order.
