# Research: Unlock rules

> Written on 2026-09-27, after the feature merged (#131), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-24

---

## D1 - One rule method beside `EnsureMayStop`

**Decision**: `ModerationRules.EnsureMayRelease(ICurrentUser caller, User target, DateTime now)`, a static
method in the same class, in the same order as `EnsureMayStop`: self, then role, then reach.

**Rationale**: Specs/043 put the rules that depend on the target's row in `ModerationRules`, because an
attribute runs before the target is read. Unlocking's rules are the same kind; keeping them side by side
makes the asymmetry that caused #121 visible.

**Alternatives considered**:

- **A role attribute on the endpoint.** Rejected: specs/043 already records why target rules are not
  attributes - an attribute runs before the target is read and cannot know who it is.
- No other alternative is recorded.

---

## D2 - "Could have set" is read from the time still to run

**Decision**: A moderator may lift a lock only when `LockedUntil - now <= ModeratorMaxLockDays` (30 days).

**Rationale**: It needs no column and no migration, and a long lock stays until an administrator decides. A
40-day lock set by an administrator becomes a moderator's to lift once 30 or fewer days remain; by then it is
the same lock the moderator could have set that day. The pull request records this as decided on the user's
behalf.

**Alternatives considered**:

- **A `LockedByRole` (or `LockedBy`) column.** Rejected: more precise, but it costs a migration for a
  distinction the remaining-time rule already draws for every lock a moderator could not have made.
- **Read who set the lock from the audit log.** Rejected: the log lives in the Activity service, and reading
  it to decide a permission would cross a service boundary (Principle I).

---

## D3 - Self is refused before the state is looked at

**Decision**: `EnsureMayRelease` runs first in the handler, before `if (user.LockedUntil is not null)`, and
refuses self with `ConflictException` ("You cannot unlock your own account.") whether or not the caller is
locked.

**Rationale**: An answer that depended on state would tell a caller whether they are locked; a fixed answer
says nothing. It is also the case #112 made dangerous: a locked moderator whose token outlived the lock.
409 matches what locking yourself answers.

**Alternatives considered**:

- **Refuse self only when locked.** Rejected for the reason above (the pull request: "refused whether or not
  you are locked, so the answer says nothing about the account's state").
- No other alternative is recorded.
