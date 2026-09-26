# Implementation Plan: Locking again obeys the unlock rule

**Branch**: `088-relock-limits` | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md) | **Issue**: #180

## Summary

Add `ModerationRules.EnsureMayShorten` beside `EnsureMayStop` and `EnsureMayRelease`, and call it in
`Handle(LockUserCommand)` after `EnsureMayStop` and before anything is staged. A lock that would end sooner than
the one in place is refused (403) for a moderator when that lock has more than 30 days still to run. No column,
no migration, no contract change, no page change.

## Technical Context

- `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs` - `ModerationRules`
  and the handlers.
- `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`.
- `bruno/admin-users/`.

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MediatR 12.4.1, `Ecommerce.Shared` (`ICurrentUser`, `ForbiddenException`)

**Storage**: none new - `users.LockedUntil` (PostgreSQL, `ecommerce_identity_db`, 5435)

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Identity.Tests`); three Bruno requests through the
gateway; mutation checks

**Target Platform**: Identity (5056) through the gateway (5000)

**Performance Goals**: none - one comparison on a row already loaded

**Constraints**: the rule depends only on the caller and the target's row; a refusal writes and publishes nothing

**Scale/Scope**: one static method, one call, three tests, three Bruno requests

## Design

```csharp
public static void EnsureMayShorten(ICurrentUser caller, User target, DateTime newUntil, DateTime now)
{
    if (caller.IsInRole(RoleNames.Admin) || target.LockedUntil is not { } until || newUntil >= until)
        return;

    if (until - now > TimeSpan.FromDays(ModeratorMaxLockDays))
        throw new ForbiddenException("Only an administrator can shorten a lock with more than 30 days to run.");
}
```

In the handler, `now + days` is computed once and used both for the check and for `LockedUntil`, so the date
checked is the date written. The call sits after `EnsureMayStop` (self, administrator, moderator's moderator) and
before `Snapshot(user)`, so a refusal has staged no audit entry, notice, email or `AccessTokensRevoked`, and the
refresh tokens are not revoked.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity decides from its own `users` row; no call to another service, no read of Activity's audit log to learn who set the lock (research D2). |
| **II. Clean Architecture Layering** | **Pass.** The rule is in the Application layer beside the two rules it completes; the controller, Domain and Infrastructure are unchanged. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The rule runs before anything is staged, so a refusal writes and publishes nothing; an allowed lock still stages its audit entry, notice, email and revocation before the one `SaveChangesAsync`, exactly as before. |
| **IV. Identity Comes From the Token** | **Pass.** The caller and their role come from `ICurrentUser`; the only request inputs are the route's target id and the body's days and reason, as before. |
| **V. Evidence Over Assumption** | **Pass.** The refusal test failed before the fix (1 red of 14); three mutations - call removed, administrator bypass removed, reach condition removed - each turned a different test red; the suite is 177/177; Bruno 275/275 against a rebuilt Identity container. |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/088-relock-limits/
├── spec.md
├── plan.md              # This file
├── research.md          # D1-D4
├── data-model.md        # No schema change; what the rule reads, the state diagram
├── quickstart.md
├── contracts/
│   └── http-api.md      # POST /api/users/{id}/lock, one new refusal
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`
- `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`
- `bruno/admin-users/register an account for a long lock.yml`, `an administrator locks it for 300 days.yml`,
  `a moderator cannot shorten the lock by locking again.yml` (seq 17-19; the later requests renumbered by 3)
- `CLAUDE.md`, `docs/features/moderation-and-staff.md`, `docs/project/backlog.md`, `docs/project/timeline.md`

No endpoint, message, table or gateway route changes, so `docs/reference/` is not regenerated.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- Two staff locking the same account at once still resolve last-write-wins (spec, Edge Cases).
- Who set a lock is still not stored; the rule does not need it (specs/050 D2).
