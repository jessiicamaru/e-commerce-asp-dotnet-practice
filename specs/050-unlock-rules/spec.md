# Feature Specification: Unlock rules

> Completed on 2026-09-27, after the feature merged (#131), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature Branch**: `050-unlock-rules` | **Created**: 2026-09-24 | **Issue**: #121

**Status**: Merged (#131, 2026-09-24)

**Input**: Issue #121 - `UnlockUserCommand` applies none of the target rules that locking applies.

## Why

Specs/043 gave locking three rules that depend on the target (`ModerationRules.EnsureMayStop`):
- nobody stops themselves;
- nobody stops an administrator;
- a moderator does not stop a moderator.

It also capped a moderator's lock at 30 days. Unlocking got none of these. `Handle(UnlockUserCommand)`
loads the account and clears the lock. The results:

- a moderator can lift a year-long lock an administrator set;
- a moderator can unlock another moderator, whom they could not have locked;
- a locked moderator whose access token still has minutes to live (#112) can unlock themselves. The
  lock ends their sessions, but not the token already in hand.

## User Scenarios & Testing *(mandatory)*

### US1 - Unlocking obeys the same limits as locking (Priority: P1)

A moderator can undo what a moderator could have done, and no more. An administrator's long lock stays
until an administrator lifts it, and nobody frees themselves.

**Why this priority**: It is the defect. Each gap lets a lesser role undo a decision it could not have made,
and one of them lets a stopped moderator free themselves.

**Independent Test**: As a moderator, try each of the three refused unlocks and one allowed unlock; as an
administrator, unlock the long lock. Check the lock columns and the audit log after each.

**Acceptance Scenarios**:

1. Nobody unlocks their own account: 409, the same answer as locking yourself.
2. A moderator cannot unlock a moderator: 403 "Only an administrator can unlock a moderator."
3. A moderator cannot lift a lock with more than 30 days still to run: 403. A lock that long is
   longer than a moderator could set, so it is an administrator's decision to undo.
4. A moderator can still unlock a customer or seller whose lock has 30 days or fewer to run.
5. An administrator can unlock anybody except themselves.
6. A refused unlock changes nothing and records no audit entry.

---

### US2 - The users page does not offer what the server refuses (Priority: P2)

"Unlock" is shown disabled in the same cases: your own account, a moderator for a moderator, and a
long lock for a moderator. This is drawing only (CLAUDE.md, "a role on the response is for DRAWING"),
because the server refuses on its own.

**Why this priority**: The server already refuses after US1; this only stops the page offering a button that
can only fail.

**Independent Test**: Render `/admin/users` as a moderator with a list holding themselves, a moderator, a
customer locked for a year and one locked for a week: only the last row's "Unlock" is enabled. As an
administrator, every row but their own is.

**Acceptance Scenarios**:

1. **Given** a moderator on the users page, **When** they open the menu of their own row, of a moderator's
   row, or of a lock with more than 30 days to run, **Then** "Unlock" is disabled.
2. **Given** an administrator, **When** they open any row but their own, **Then** "Unlock" is enabled.

### Edge Cases

- **Unlocking yourself when you are not locked.** Still 409: the answer does not depend on the account's
  state, so it reveals nothing about it.
- **Exactly 30 days to run.** Within a moderator's reach; the rule refuses only *more* than 30 days.
- **A lock an administrator set for 40 days.** A moderator's to lift once 30 or fewer days remain - by then it
  is the same lock the moderator could have set that day.
- **A lock that has already run out.** `LockedUntil` is in the past, so it is within reach; the unlock clears
  it.
- **Unlocking a banned account.** Clears only the lock; a ban stays until an administrator lifts it
  (`ModerationTests.A_ban_holds_until_lifted`).
- **The page's clock.** "Days still to run" is measured from when the page opened, not re-read in render.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `ModerationRules.EnsureMayRelease(caller, target, now)` holds the rules and is called
  before anything changes.
- **FR-002**: The rule reads the row (`LockedUntil`), with no new column and no migration.
- **FR-003**: Self is refused with `ConflictException` (409); a moderator's refusals are `ForbiddenException`
  (403) whose message says what only an administrator may do.
- **FR-004**: A refused unlock writes nothing - no column change, no audit entry.
- **FR-005**: The users page disables "Unlock" in exactly the cases the server refuses, and is not relied on
  for them.

### Key Entities

- **Account lock**: the `LockedUntil` / `LockReason` pair on Identity's `users` row (specs/043). The time still
  to run is `LockedUntil - now`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Each of the three refusals is covered by a test that failed before the fix and passes after.
- **SC-002**: A refused unlock leaves `LockedUntil` set and publishes no audit entry, verified by the tests.
- **SC-003**: End to end through the gateway, a moderator cannot unlock themselves (409) or lift a 365-day lock
  (403), and an administrator can.

## Decision

**"Longer than a moderator could set" is measured on the time still to run, not on who set the lock.**
Knowing who set it would need a new column (`LockedBy`) and a migration, or a read of the audit log in
another service. The remaining-time rule protects what matters: a long lock stays until an
administrator decides. It also lets a moderator end a lock that has already run down to within their
own reach. Rejected: a `LockedByRole` column. It is more precise, but it costs a migration for a
distinction the remaining-time rule already draws for every lock a moderator could not have made.

Recorded in the pull request as decided on the user's behalf; see [research.md](research.md) D2.

## Assumptions

- The 30-day cap (`ModerationRules.ModeratorMaxLockDays`) from specs/043 is the measure of "what a moderator
  could have set".
- The controller's `[Authorize(Roles = StaffRoles.Staff)]` on the unlock endpoint is unchanged: customers and
  sellers never reach the rule.

## Out of scope

- #112, the access token outliving the lock. (Closed later by specs/065.)
- Lifting a ban, which is administrator-only already, at the controller.
