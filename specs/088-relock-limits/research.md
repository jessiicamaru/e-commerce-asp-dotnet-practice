# Research: Locking again obeys the unlock rule

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #180

---

## D1 - Refuse a shortening lock; do not silently keep the longer one

**Decision**: When a moderator's lock would end sooner than a lock they could not have lifted, throw
`ForbiddenException` before anything is staged.

**Rationale**: The handler does more than set a column. An allowed lock publishes an `AccountLocked` audit entry,
a notice and an email carrying the new end date, `AccessTokensRevoked`, and revokes every refresh token. A silent
`max(existing, new)` would either do all of that for a change that did not happen - telling the account's owner
"locked until tomorrow" while the lock runs 300 days - or need each side effect skipped by hand, which is the kind
of branch that is forgotten in the next edit. A 403 is one line, changes nothing, and uses the wording the unlock
refusal (specs/050) already uses, so a moderator meets one rule in two places.

**Alternatives considered**:

- **`max(existing, new)` with 200** (the issue's "simplest version"). Rejected for the reason above; the issue
  itself accepts either outcome "with its outcome stated".
- **409 Conflict.** Rejected: nothing about the account's state is wrong; the caller lacks the authority, which
  is what 403 means here, and what `EnsureMayRelease` answers for the same situation.

---

## D2 - A moderator may shorten a lock that is within their reach

**Decision**: The rule applies only when the lock in place has more than `ModeratorMaxLockDays` still to run.
A moderator may replace a 20-day lock with a 2-day one.

**Rationale**: Specs/050 lets a moderator lift such a lock outright and lock again for 2 days. Refusing the one
step they may take in two protects nothing and would make the lock endpoint stricter than the unlock endpoint.
The rule is phrased as "a shortening is a partial release", so it inherits the release rule exactly.

**Alternatives considered**:

- **A moderator's lock only ever extends.** Rejected: a rule bypassed by unlock-then-lock is ceremony.
- **Record who set the lock (`LockedBy`) and compare roles.** Rejected, as in specs/050 D2: a migration for a
  distinction the remaining-time rule already draws.

---

## D3 - The rule is a third `ModerationRules` method, called after `EnsureMayStop`

**Decision**: `ModerationRules.EnsureMayShorten(caller, target, newUntil, now)`, called in
`Handle(LockUserCommand)` after `EnsureMayStop` and before the snapshot.

**Rationale**: Specs/043 put every rule that depends on the target's row in `ModerationRules`; specs/050 added
the release rule beside it. #180 happened because the two paths that change a lock did not share a rule. Placing
the third beside the other two keeps the relation visible on one screen. `EnsureMayStop` already refuses self
and a moderator's moderator, so the new method only adds the reach condition.

**Alternatives considered**:

- **Call `EnsureMayRelease` whenever the new date is earlier.** Rejected: it would answer "You cannot unlock your
  own account" and "Only an administrator can lift a lock ..." to a lock request - true in substance, confusing
  in words. The shared part is the reach condition, one line.
- **Enforce it in the validator.** Rejected: a validator runs before the target is read.

---

## D4 - The page needs no change

**Decision**: `client/src/pages/admin-users` is not changed.

**Rationale**: The row menu shows "Unlock" when `lockedUntil` is set and "Lock" otherwise, so the page never
offers a re-lock; the refusal is reachable only through the API. Adding a disabled item for something the page
does not offer would draw a rule that has no button.

**Alternatives considered**:

- **Offer "Lock again" with a `relockable` flag.** Rejected: a feature nobody asked for, added only to disable it.
