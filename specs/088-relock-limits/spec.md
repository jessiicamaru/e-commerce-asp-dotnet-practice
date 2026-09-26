# Feature Specification: Locking again obeys the unlock rule

**Feature Branch**: `088-relock-limits` | **Created**: 2026-09-27 | **Issue**: #180

**Status**: Draft

**Input**: Issue #180 - "a moderator can shorten an administrator's lock by locking the account again".

## Why

Specs/043 lets staff lock an account until a date; a moderator for at most 30 days. Specs/050 (#121) made
**unlocking** obey the same limits: a moderator lifts only a lock with at most 30 days still to run, because a
longer one is an administrator's decision.

Locking again undoes that by another route. `Handle(LockUserCommand)` checks who the target is
(`EnsureMayStop`) and then **replaces** `LockedUntil` with `now + days`. So:

1. an administrator locks an account for 300 days;
2. a moderator locks the same account for 1 day;
3. the account is free tomorrow - exactly the unlock specs/050 refuses, reached through the lock endpoint.

The audit log records it, so it is not silent, but it is a lesser role undoing a decision it could not have
made, and the log is read after the fact.

## User Scenarios & Testing *(mandatory)*

### US1 - A lock never ends sooner than one the caller could not have lifted (Priority: P1)

A moderator who locks an account that already has a lock is refused when their lock would end **sooner** and
the lock in place has more than 30 days still to run. Everything else a lock could do before is unchanged.

**Why this priority**: It is the defect, and the only story with a change. It closes the one remaining route by
which a moderator shortens an administrator's lock.

**Independent Test**: Lock an account for 300 days as an administrator; lock it for 1 day as a moderator
(refused, nothing changed, nothing recorded or sent); lock it for 1 day as an administrator (allowed). Separately,
as a moderator lock for 5 days, then 20 (extended), then 2 (allowed, because 20 days is within a moderator's
reach).

**Acceptance Scenarios**:

1. **Given** an account locked for 300 days by an administrator, **When** a moderator locks it for 1 day,
   **Then** the answer is 403 "Only an administrator can shorten a lock with more than 30 days to run.",
   `LockedUntil` and `LockReason` are unchanged, and no audit entry, notice or email is published.
2. **Given** the same account, **When** an administrator locks it for 1 day, **Then** it is locked for 1 day and
   the audit entry shows the lock and reason before and after.
3. **Given** an account a moderator locked for 5 days, **When** the moderator locks it for 20 days, **Then** it is
   locked for 20 days (extending is always a lock the moderator could set).
4. **Given** an account with a lock of 20 days to run, **When** a moderator locks it for 2 days, **Then** it is
   locked for 2 days: the moderator could have unlocked it (specs/050) and locked it for 2.
5. **Given** an account whose lock has already run out, **When** a moderator locks it, **Then** it is locked as
   for an account never locked.

---

### US2 - The users page offers nothing the server now refuses (Priority: P3)

**No change is needed, and none is made.** `/admin/users` offers "Unlock" for a locked account and "Lock" only for
one that is not locked (`a.lockedUntil ? Unlock : Lock`), so the page never offers a re-lock. The refusal is
reachable only through the API, which is where it is now held. Recorded so that nobody adds a "lock again" item
without the same condition as "Unlock" (`releasable`).

**Why this priority**: Nothing to build; the story exists so the reasoning is on record.

**Independent Test**: The existing page tests (`client/src/pages/admin-users/index.test.tsx`) stay green unchanged.

**Acceptance Scenarios**:

1. **Given** a locked account on `/admin/users`, **When** staff open its menu, **Then** it offers "Unlock", not
   "Lock" (unchanged).

### Edge Cases

- **The same end date.** A re-lock that ends at the same instant or later is not a shortening; allowed.
- **Exactly 30 days to run.** Within a moderator's reach, as for unlocking (specs/050): the rule refuses only
  *more* than 30.
- **A moderator locking a moderator, or themselves.** Refused before this rule by `EnsureMayStop` (403 / 409),
  unchanged.
- **A banned account.** Locking it is unchanged by this feature; the ban is a separate pair of columns.
- **A refused re-lock and sessions.** The refusal comes before any change, so it ends no session and revokes no
  token - the account's owner is not signed out by an attempt that did nothing.
- **Two staff locking at once.** Not made safer or worse: the row is read and saved by EF as before (last write
  wins). Out of scope; each outcome is a lock its caller was allowed to set against the row they read.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A lock that would end sooner than the account's current `LockedUntil` is allowed only when the
  caller could have lifted that lock under specs/050's rule: an administrator always; a moderator only when the
  current lock has at most `ModeratorMaxLockDays` (30) days still to run.
- **FR-002**: The rule lives beside `EnsureMayStop` and `EnsureMayRelease` in `ModerationRules`
  (`EnsureMayShorten`), and runs before anything is staged.
- **FR-003**: A refusal is `ForbiddenException` (403) whose message says what only an administrator may do.
- **FR-004**: A refused lock changes no column and publishes nothing: no audit entry, no notice, no email, no
  `AccessTokensRevoked`, and no session is revoked.
- **FR-005**: An allowed lock behaves exactly as before, including its audit entry with the before/after
  snapshot (lock and reason).
- **FR-006**: No column, no migration, no contract change.

### Key Entities

- **Account lock**: `users.LockedUntil` / `LockReason` (specs/043). "Time still to run" is `LockedUntil - now`;
  "sooner" is `now + days < LockedUntil`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `A_moderator_does_not_shorten_a_lock_by_locking_again` fails before the fix and passes after.
- **SC-002**: A refused re-lock publishes no audit entry and no notice and leaves `LockedUntil` and `LockReason`
  as they were, asserted by that test.
- **SC-003**: The allowed cases (administrator shortens, moderator extends, moderator shortens within reach) are
  held by tests, so the fix cannot be a blanket refusal.
- **SC-004**: Through the gateway, the Bruno request `a moderator cannot shorten the lock by locking again`
  answers 403.
- **SC-005**: Each part of the rule is shown load-bearing by a mutation (rule not called; administrator bypass
  removed; reach condition removed) that turns a test red.

## Decision

**Refuse, rather than silently keep the longer lock (`max(existing, new)`).** The issue offered both. A silent
max would return 200 while doing something other than asked, and would still publish a notice and an email saying
"locked until" a date, revoke the account's tokens and write an audit entry for a change that did not happen -
or need each of those skipped by hand. A 403 tells the moderator why, in the same words as the unlock refusal,
and changes nothing. Recorded as decided on the user's behalf ([research.md](research.md) D1).

**Reach is measured the way specs/050 measures it** - time still to run, not who set the lock - so a moderator may
shorten a lock they could have lifted. "A moderator only ever extends" would refuse them a change they can already
make in two steps (unlock, then lock): a rule that protects nothing (D2).

## Assumptions

- The 30-day cap (`ModerationRules.ModeratorMaxLockDays`) is the measure of a moderator's reach, as in specs/043
  and specs/050.
- The lock endpoint's `[Authorize(Roles = StaffRoles.Staff)]` is unchanged; customers and sellers never reach
  the rule.

## Out of scope

- Concurrent locks by two staff (see Edge Cases).
- Recording who set a lock (`LockedBy`) - specs/050 D2 rejected it and nothing here needs it.
- Bans, which only an administrator sets or lifts.
