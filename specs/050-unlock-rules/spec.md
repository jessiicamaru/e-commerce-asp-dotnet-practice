# Feature Specification: Unlock rules

**Feature Branch**: `050-unlock-rules` | **Created**: 2026-09-24 | **Issue**: #121

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

## User Scenarios

### US1 - Unlocking obeys the same limits as locking (P1)

**Acceptance**
1. Nobody unlocks their own account: 409, the same answer as locking yourself.
2. A moderator cannot unlock a moderator: 403 "Only an administrator can unlock a moderator."
3. A moderator cannot lift a lock with more than 30 days still to run: 403. A lock that long is
   longer than a moderator could set, so it is an administrator's decision to undo.
4. A moderator can still unlock a customer or seller whose lock has 30 days or fewer to run.
5. An administrator can unlock anybody except themselves.
6. A refused unlock changes nothing and records no audit entry.

### US2 - The users page does not offer what the server refuses (P2)

"Unlock" is shown disabled in the same cases: your own account, a moderator for a moderator, and a
long lock for a moderator. This is drawing only (CLAUDE.md, "a role on the response is for DRAWING"),
because the server refuses on its own.

## Requirements

- **FR-001**: `ModerationRules.EnsureMayRelease(caller, target, now)` holds the rules and is called
  before anything changes.
- **FR-002**: The rule reads the row (`LockedUntil`), with no new column and no migration.

## Decision

**"Longer than a moderator could set" is measured on the time still to run, not on who set the lock.**
Knowing who set it would need a new column (`LockedBy`) and a migration, or a read of the audit log in
another service. The remaining-time rule protects what matters: a long lock stays until an
administrator decides. It also lets a moderator end a lock that has already run down to within their
own reach. Rejected: a `LockedByRole` column. It is more precise, but it costs a migration for a
distinction the remaining-time rule already draws for every lock a moderator could not have made.

## Out of scope

- #112, the access token outliving the lock.
- Lifting a ban, which is administrator-only already, at the controller.
