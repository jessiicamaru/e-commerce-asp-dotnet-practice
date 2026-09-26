---
description: "Task list for Unlock rules"
---

# Tasks: Unlock rules

> Completed on 2026-09-27, after the feature merged (#131), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - the three server tests and the page test failed before the fix.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1)

- [X] T001 [US1] Tests first in `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`: self, moderator-on-moderator, a long lock for a moderator, allowed cases, nothing recorded on refusal
- [X] T002 [US1] `ModerationRules.EnsureMayRelease` + call it in `Handle(UnlockUserCommand)` in `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`
- [X] T005 [P] [US1] Bruno `bruno/admin-users/a moderator cannot unlock their own account.yml` (409, `seq: 12`); renumber the later requests in the folder

## Phase 2: Client (US2)

- [X] T003 [US2] Test then disable "Unlock" in `client/src/pages/admin-users/index.test.tsx` / `index.tsx`
- [X] T006 [US2] Take "now" once when the page opens (`useState(() => Date.now())`) in `client/src/pages/admin-users/index.tsx`, as the linter asked

## Phase 3: Verification and docs

- [X] T004 Mutation checks; docs `docs/features/moderation-and-staff.md`, `docs/project/*`, CLAUDE.md
- [X] T007 End to end against a rebuilt Identity container through the gateway: self 409, moderator on a 365-day lock 403, administrator 200, moderator on their own 7-day lock 200
- [X] T008 Merged as #131 (2026-09-24), closing #121

## Verification recorded in #131

- Server: the three new tests failed before the fix; `Ecommerce.Identity.Tests` 79/79 against real PostgreSQL.
- Client: the new page test failed before the fix; the full suite 281/281, lint and `tsc` clean.
- Mutations, each restored: no self rule - red; no moderator rule - red; no reach rule - red; the rule never
  called - 3 red; page ignores the reach rule - red.

## Notes

T005-T008 were added on 2026-09-27 from the pull request; the work was part of #131 but had no task line.
