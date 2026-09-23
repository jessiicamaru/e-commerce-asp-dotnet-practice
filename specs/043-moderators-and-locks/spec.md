# Feature Specification: Moderators, locks and bans

**Feature Branch**: `043-moderators-and-locks` | **Created**: 2026-09-24 | **Issue**: #88

## Why

The shop has one administrator, seeded at startup, and nobody else with any staff power. Nobody can be
given a share of the work, and nobody can stop an abusive account.

## User Scenarios

### US1 - An administrator makes somebody a moderator (P1)

An administrator opens **Users** in the console, finds a person by email, and grants them **Moderator**.
The next time that person's session refreshes, they hold the role and see the console. Revoking it works
the same way.

**Acceptance**
1. An admin grants Moderator by email. After that person refreshes, their roles include Moderator.
2. Revoking removes it at the next refresh.
3. A moderator, customer or seller asking to grant or revoke gets **403**.
4. The person is told in their notifications.

### US2 - Staff stop an account (P1)

A moderator or administrator **locks** a customer or seller for a number of days, with a reason. Only an
administrator **bans**, which lasts until lifted.

While locked or banned, the person cannot sign in. The reason and the end date are shown only after the
right password, so a stranger learns nothing. Their sessions end at once, so a refresh is refused too.
Lifting the lock or ban restores access.

**Acceptance**
1. A locked user signing in with the right password gets **403** naming the end date. A refresh gets
   **401**. After unlocking, sign-in works.
2. A moderator may lock for **at most 30 days**. An administrator may lock for up to a year.
3. Only an administrator bans and lifts a ban. A moderator trying either gets 403.
4. Nobody locks or bans **themselves** or **an administrator**. A moderator cannot lock **another
   moderator**.

### US3 - Every one of these is on the record (P2)

Each grant, revoke, lock, unlock, ban and lift is an audit entry. Role changes go under **Security**;
locks and bans go under **Moderation**. Each entry has the before and after, so the diff shows exactly
what changed.

## Requirements

- **FR-001**: A Moderator role exists alongside Admin, Customer and Seller.
- **FR-002**: Staff (Admin or Moderator) list and search users by email or name, paged.
- **FR-003**: Only an Admin grants or revokes, and **Moderator is the only role this can touch**. Admin
  stays seeded, and Seller comes from opening a shop.
- **FR-004**: A lock has an end date and a reason. A ban has a reason and no end date. Both end every
  session at once.
- **FR-005**: Sign-in with the right password on a locked or banned account is refused with 403 and a
  sentence the person can read. Refresh is refused with 401.
- **FR-006**: Every service can name "Staff" as one role list, for the moderation endpoints that follow
  (#89, #90, #91).
- **FR-007**: The rules in US2 (4) hold whatever the client sends.

## Out of scope

- An access token already issued lives out its minutes (15). A lock ends sessions at the next refresh,
  not mid-request. Accepted: the alternative is a lookup on every request in every service.
- Deleting accounts.

## Success Criteria

- **SC-001**: Grant → refresh → role present, and revoke → refresh → role gone, both in one Bruno run.
- **SC-002**: A locked account cannot sign in or refresh. After unlocking it can.
- **SC-003**: Each moderation action appears once in the audit log, with its diff.
