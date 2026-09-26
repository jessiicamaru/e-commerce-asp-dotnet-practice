---
description: "Task list for Locking again obeys the unlock rule"
---

# Tasks: Locking again obeys the unlock rule

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - the refusal test failed before the fix.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1)

- [X] T001 [US1] Tests first in `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`: a moderator does not shorten a 300-day lock (403, nothing changed, recorded or sent); a moderator extends and shortens within reach; an administrator shortens any, with the audit before/after
- [X] T002 [US1] `ModerationRules.EnsureMayShorten` and its call in `Handle(LockUserCommand)` in `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`
- [X] T003 [P] [US1] Bruno `bruno/admin-users/`: register an account for a long lock, an administrator locks it for 300 days, a moderator cannot shorten it (403) - seq 17-19, later requests renumbered

## Phase 2: Page (US2)

- [X] T004 [US2] Confirm `client/src/pages/admin-users` offers no re-lock (a locked row shows "Unlock" only) - no change (research D4)

## Phase 3: Verification and docs

- [X] T005 Mutation checks (quickstart Scenario 4): call removed, administrator bypass removed, reach removed - each red
- [X] T006 Identity suite 177/177 against real PostgreSQL; Bruno 275/275 against a rebuilt Identity container
- [X] T007 Docs: `docs/features/moderation-and-staff.md`, `docs/project/backlog.md`, `docs/project/timeline.md`, CLAUDE.md
- [X] T008 Merged as #187 (2026-09-27), closing #180

## Verification

- Before the fix: `A_moderator_does_not_shorten_a_lock_by_locking_again` red (1 of 14 in `ModerationTests`).
- After: `Ecommerce.Identity.Tests` 177/177.
- Mutations, each restored: no call - the refusal test red; no administrator bypass - `An_administrator_shortens_any_lock`
  red; no reach condition - `A_moderator_extends_a_lock_and_shortens_only_one_they_could_have_lifted` red.
- Bruno, whole collection through the gateway with Identity rebuilt from this branch: 275 requests, 448 tests passed.
