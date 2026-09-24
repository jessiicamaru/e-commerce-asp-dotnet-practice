# Implementation Plan: Unlock rules

**Branch**: `050-unlock-rules` | **Spec**: [spec.md](spec.md) | **Issue**: #121

## Technical context

- `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`:
  - `ModerationRules`, which holds `EnsureMayStop` and `ModeratorMaxLockDays`;
  - the handlers.
- `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`.
- `client/src/pages/admin-users/index.tsx` and its test.

## Design

- `EnsureMayRelease(ICurrentUser caller, User target, DateTime now)`. The order matches
  `EnsureMayStop`:
  - self → `ConflictException`;
  - a moderator releasing a moderator → `ForbiddenException`;
  - a moderator releasing a lock with more than `ModeratorMaxLockDays` to run → `ForbiddenException`.
- It is called first in `Handle(UnlockUserCommand)`, before the lock check, so asking to unlock
  yourself is refused even when you are not locked. The answer does not depend on state.
- Client: a `releasable` flag beside `stoppable`, with the same three conditions.

## Constitution check

- IV (identity from the token): the caller comes from `ICurrentUser`. Pass.
- V (evidence): one failing test per rule before the fix, then mutation checks. Pass.
- No migration, no contract change, no new cross-service call.
