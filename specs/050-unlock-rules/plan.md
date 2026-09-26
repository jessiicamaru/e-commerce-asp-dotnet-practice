# Implementation Plan: Unlock rules

> Completed on 2026-09-27, after the feature merged (#131), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Branch**: `050-unlock-rules` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #121

## Summary

Add `ModerationRules.EnsureMayRelease` beside `EnsureMayStop` and call it first in
`Handle(UnlockUserCommand)`, so unlocking refuses yourself (409), a moderator unlocking a moderator (403) and
a moderator lifting a lock with more than 30 days to run (403). The users page disables "Unlock" in the same
cases. No column, no migration, no contract change.

## Technical context

- `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`:
  - `ModerationRules`, which holds `EnsureMayStop` and `ModeratorMaxLockDays`;
  - the handlers.
- `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`.
- `client/src/pages/admin-users/index.tsx` and its test.

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: MediatR 12.4.1, `Ecommerce.Shared` (`ICurrentUser`, `ConflictException`,
`ForbiddenException`); client TanStack Query, shadcn `dropdown-menu`

**Storage**: none new - `users.LockedUntil` (PostgreSQL, `ecommerce_identity_db`, 5435)

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Identity.Tests`); Vitest page test; one Bruno request

**Target Platform**: Identity (5056) through the gateway; the storefront's `/admin/users`

**Constraints**: the rule must depend only on the caller and the target's row; a refusal must write nothing

**Scale/Scope**: one static method, one call, one page flag

## Design

- `EnsureMayRelease(ICurrentUser caller, User target, DateTime now)`. The order matches
  `EnsureMayStop`:
  - self → `ConflictException`;
  - a moderator releasing a moderator → `ForbiddenException`;
  - a moderator releasing a lock with more than `ModeratorMaxLockDays` to run → `ForbiddenException`.
- It is called first in `Handle(UnlockUserCommand)`, before the lock check, so asking to unlock
  yourself is refused even when you are not locked. The answer does not depend on state.
- Client: a `releasable` flag beside `stoppable`, with the same three conditions.

At the merge the page computes `withinReach` from `lockedUntil` and a `now` taken once when the page opens
(`useState(() => Date.now())`) - the linter flagged `Date.now()` in render - and
`releasable = !self && (isAdmin || (!moderator && withinReach))`.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md); re-checked after design.*

- IV (identity from the token): the caller comes from `ICurrentUser`. Pass.
- V (evidence): one failing test per rule before the fix, then mutation checks. Pass.
- No migration, no contract change, no new cross-service call.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity decides from its own row; the rejected alternative of reading who set the lock from Activity's audit log would have been a read of another service's data (research D2) |
| **II. Clean Architecture Layering** | **Pass.** The rule is in the Application layer beside `EnsureMayStop`; the controller is unchanged |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The rule runs before any change, so a refusal stages nothing; an allowed unlock still records its audit entry and saves once, as before |
| **IV. Identity Comes From the Token** | **Pass.** The caller and their roles come from `ICurrentUser`; the target id is the route's, and nothing about the caller is read from the request |
| **V. Evidence Over Assumption** | **Pass.** Three server tests and one page test failed before the fix; five mutations were each caught; the four end-to-end steps were run against a rebuilt Identity container |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/050-unlock-rules/
├── spec.md
├── plan.md            # This file
├── research.md        # D1-D3
├── data-model.md      # No schema change; the columns the rule reads
├── quickstart.md
├── contracts/
│   └── http-api.md    # POST /api/users/{id}/unlock, new refusals
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`
- `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`
- `client/src/pages/admin-users/index.tsx`, `index.test.tsx`
- `bruno/admin-users/a moderator cannot unlock their own account.yml` (new, `seq: 12`; later requests renumbered)
- `CLAUDE.md`, `docs/features/moderation-and-staff.md`, `docs/project/backlog.md`, `docs/project/timeline.md`

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- A locked moderator's access token still worked for up to 15 minutes (#112); this feature only stops that
  token from being used to unlock. Specs/065 closed #112.
- Who set a lock is still not recorded on the row; the rule does not need it (research D2).
