# Feature Specification: Moderators, locks and bans

> Completed on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature Branch**: `043-moderators-and-locks` | **Created**: 2026-09-24 | **Issue**: #88

**Status**: Merged (#95, 2026-09-23 20:46 UTC, which was 2026-09-24 in the author's time zone)

**Input**: Issue #88 - "There is one administrator, seeded at startup, and nothing else: no way to give
anybody else any staff power, and no way to stop an abusive account." It asked for a Moderator role granted
by email from a Users page, a lock (until a date) and a ban (until lifted) with a reason, a Staff policy
every service can name, and an audit entry with a diff for every one of those actions. Decided with the
user on 2026-09-24: a moderator may lock a customer or seller for up to 30 days; only an administrator
bans, lifts a ban, or grants and revokes roles.

## Why

The shop has one administrator, seeded at startup, and nobody else with any staff power. Nobody can be
given a share of the work, and nobody can stop an abusive account.

Before this feature `RoleNames` held three roles - `Admin`, `Customer`, `Seller` - and Identity's
controllers exposed register, login, refresh, logout, addresses and a seller's own shop name. No endpoint
granted or revoked a role, and none locked an account. The moderation work that was already queued behind
this issue - shop applications (#89), product review (#90) and review hiding (#91) - had nobody but the
one administrator to do it.

## User Scenarios & Testing *(mandatory)*

### US1 - An administrator makes somebody a moderator (Priority: P1)

An administrator opens **Users** in the console, finds a person by email, and grants them **Moderator**.
The next time that person's session refreshes, they hold the role and see the console. Revoking it works
the same way.

**Why this priority**: every other part of the feature needs somebody besides the one administrator to hold
staff power. Without a way to grant the role there is no second pair of hands, and the moderation queues
that follow (#89, #90, #91) have nobody to work them.

**Independent Test**: register a person, grant them Moderator as the administrator, sign them in (or
refresh their session) and read `Moderator` in the roles on the authentication response; revoke it and
refresh again, and it is gone.

**Acceptance Scenarios**

1. **Given** a registered customer, **When** an admin grants Moderator by email, **Then** after that
   person refreshes, their roles include Moderator.
2. **Given** a moderator, **When** an admin revokes the role, **Then** revoking removes it at the next
   refresh.
3. **Given** a moderator, customer or seller, **When** they ask to grant or revoke, **Then** they get
   **403**.
4. **Given** a grant or a revoke has happened, **When** the person opens their notifications, **Then** the
   person is told in their notifications (`ModeratorGranted`, linking to `/admin`, or `ModeratorRevoked`).
5. **Given** any account, **When** an administrator asks to grant `Admin`, `Seller` or `Customer`,
   **Then** the request is refused with 400, because Moderator is the only role this can touch.
6. **Given** a banned account, **When** an administrator grants it Moderator, **Then** the request is
   refused with 409 and nothing changes.

---

### US2 - Staff stop an account (Priority: P1)

A moderator or administrator **locks** a customer or seller for a number of days, with a reason. Only an
administrator **bans**, which lasts until lifted.

While locked or banned, the person cannot sign in. The reason and the end date are shown only after the
right password, so a stranger learns nothing. Their sessions end at once, so a refresh is refused too.
Lifting the lock or ban restores access.

**Why this priority**: an abusive account is the other half of what #88 found missing, and it is equal in
weight to US1: without it the shop's only response to abuse is editing the database by hand.

**Independent Test**: sign a customer in, lock them as a moderator, then try to sign in with the right
password (403 with the reason), with a wrong password (the ordinary 401) and to refresh the earlier
session (401); unlock them and sign in again.

**Acceptance Scenarios**

1. **Given** a customer with a live session, **When** staff lock them, **Then** a locked user signing in
   with the right password gets **403** naming the end date, and a refresh gets **401**. After unlocking,
   sign-in works.
2. **Given** a moderator, **When** they lock an account, **Then** a moderator may lock for **at most 30
   days**; asking for 31 is 403. An administrator may lock for up to a year (365 days).
3. **Given** a moderator, **When** they try to ban or to lift a ban, **Then** only an administrator bans
   and lifts a ban, and a moderator trying either gets 403.
4. **Given** any member of staff, **When** they try to lock or ban their own account or an
   administrator's, **Then** nobody locks or bans **themselves** or **an administrator** (409); **and when**
   a moderator tries to lock **another moderator**, **Then** it is refused with 403 - an administrator may.
5. **Given** a locked account, **When** somebody signs in with the wrong password, **Then** the answer is
   the same 401 "Invalid email or password." as for any wrong password (#28).
6. **Given** a banned account, **When** staff unlock it, **Then** it stays banned: unlocking is not lifting
   a ban. **When** an administrator lifts the ban, **Then** the person signs in.

---

### US3 - Every one of these is on the record (Priority: P2)

Each grant, revoke, lock, unlock, ban and lift is an audit entry. Role changes go under **Security**;
locks and bans go under **Moderation**. Each entry has the before and after, so the diff shows exactly
what changed.

**Why this priority**: the audit log (specs/041) already existed and every change that matters was being
recorded in it. Staff power without a record of who used it on whom would be the one gap in it. It is P2
because it adds no capability of its own - it records the capabilities of US1 and US2.

**Independent Test**: grant Moderator and lock an account, then read the audit log: one `RoleGranted`
under Security whose after-snapshot holds Moderator and whose before does not, and one `AccountLocked`
under Moderation carrying the reason.

**Acceptance Scenarios**

1. **Given** an administrator grants Moderator, **When** the audit log is read, **Then** there is exactly
   one `Security` / `RoleGranted` entry, Moderator absent from its before and present in its after.
2. **Given** a moderator locks and then unlocks a customer, **When** the audit log is filtered to that
   customer under Moderation, **Then** there is one `AccountLocked` and one `AccountUnlocked`, each with
   the moderator as the actor role.
3. **Given** a locked or banned person signs in with the right password, **When** the audit log is read,
   **Then** the refusal is recorded under Security as `SignInRefused`.

---

### US4 - Each member of staff works from one console (Priority: P3)

A moderator opens the same console an administrator uses, at `/admin`, but sees only the pages a
moderator can use and lands on Users. On Users they search, read each person's roles and whether they are
locked or banned, and pick an action from a row menu that offers only what their role allows.

**Why this priority**: the endpoints of US1 and US2 are usable from Bruno or curl without it. It is what
makes them usable by a person, so it follows them.

**Independent Test**: sign in as a moderator, open `/admin`, and see only Users in the sidebar; open a
customer's row menu and find Lock but no Grant and no Ban; open the lock dialog and find no length above
30 days.

**Acceptance Scenarios**

1. **Given** a moderator, **When** they open `/admin`, **Then** they are sent to `/admin/users` and the
   sidebar shows Users only; an administrator sees Fulfilment, Payouts, Users and Audit.
2. **Given** a moderator on Users, **When** they open a row menu, **Then** it offers no grant, no revoke
   and no ban, and Lock is disabled on their own row, an administrator's and another moderator's.
3. **Given** a moderator locking somebody, **When** the dialog opens, **Then** it offers 1, 3, 7, 14 and
   30 days and requires a reason; an administrator is also offered 90 and 365.
4. **Given** the server refuses an action, **When** the page shows it, **Then** the refusal is shown in the
   server's own words.

---

### Edge Cases

- **A lock that has run out.** It is no lock: `IsLocked(now)` is `LockedUntil > now`, the person signs in
  again without anybody clearing the column, and staff see no lock on the page.
- **A lock and a ban at once.** Both can hold; the ban wins the sign-in message, and unlocking leaves the
  ban in place.
- **Granting a role already held, revoking one not held, unlocking an account not locked, lifting a ban
  that is not there.** Each answers 200 with the account as it is, writes nothing and records nothing.
- **Locking an account that is already locked.** The new lock replaces the old one: `LockedUntil` becomes
  now plus the new number of days, whether that is longer or shorter. Observed in the code; the record
  does not say whether this was considered.
- **A moderator asking for more than 30 days on an unknown id.** The cap is checked before the target is
  read, so the answer is 403, not 404.
- **An unknown user id** on any write is 404 "User not found.".
- **A role name other than Moderator** in the grant or revoke path is 400 with the validator's sentence.
- **A session that escaped revocation.** Refresh reads the account row, so it is refused whatever token it
  presents.
- **An access token already issued** keeps working until it expires (at most 15 minutes) - see Out of
  scope.
- **A wrong password on a locked account** says only "wrong", exactly as on any other account (#28).

## Requirements *(mandatory)*

### Functional Requirements

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
- **FR-008**: A moderator MUST NOT lock for more than 30 days; nobody MAY lock for fewer than 1 or more
  than 365. A lock and a ban MUST carry a reason of at most 500 characters that is not blank.
- **FR-009**: Before the password is checked, a locked or banned account MUST be indistinguishable from any
  other account at sign-in (#28).
- **FR-010**: A grant or a revoke MUST tell the person through their notifications.
- **FR-011**: Every grant, revoke, lock, unlock, ban and lift MUST be recorded in the audit log with a
  before and after snapshot of the roles and the two stops, committed with the change itself. A refused
  sign-in on a stopped account MUST be recorded too.
- **FR-012**: A banned account MUST NOT be given a role.
- **FR-013**: A repeated grant, revoke, unlock or lift MUST change nothing and record nothing.
- **FR-014**: A 403 raised by these rules MUST carry a sentence the caller can read; "not yours" stays a
  404 everywhere else (specs/027).
- **FR-015**: The console MUST draw for Admin or Moderator and show each only the pages and actions their
  role can use; the server MUST refuse each of those on its own.

### Key Entities

- **Account (user)**: a person who signs in. Now also carries an optional lock (until when, and why) and
  an optional ban (since when, and why). Locked and banned are not a status; they are facts that can both
  hold, and a lock that has run out is simply over.
- **Role**: Admin, Customer, Seller and now Moderator. A person may hold several. Only Moderator is granted
  or revoked by a person; the others come from seeding, registration or an approved shop.
- **Session (refresh token)**: what keeps a person signed in. Stopping an account ends every one of them.
- **Audit entry**: who did what to whom, with the before and after (specs/041).
- **Notification**: what a person is told happened to them (specs/042).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Grant → refresh → role present, and revoke → refresh → role gone, both in one Bruno run.
  *Corrected 2026-09-27:* the Bruno run in #95 proves the grant by **signing in** and reading the role, and
  the revoke from the revoke's own response; the refresh-after-revoke half is proven by
  `ModerationTests.An_administrator_grants_moderator_and_it_arrives_at_the_next_refresh`, not by Bruno.
- **SC-002**: A locked account cannot sign in or refresh. After unlocking it can.
- **SC-003**: Each moderation action appears once in the audit log, with its diff.
- **SC-004**: Each rule that decides who may stop whom fails a test when it is removed: the 30-day cap, the
  moderator-on-moderator guard and the sign-in refusal were each mutated in #95 and each turned a test red.
- **SC-005**: The Users page is usable at a phone's width - no horizontal overflow at 390px (screenshot in
  #95).

## Assumptions

- An access token lives 15 minutes (`JwtSettings:ExpiryMinutes` in Identity's `appsettings.json`) and a
  refresh token 7 days; the refresh token travels in an HttpOnly cookie.
- Refresh already re-read the user's roles from the database, so a role granted or revoked reaches a
  session at its next refresh with no extra work.
- The audit log (specs/041) and in-app notifications (specs/042) exist and are reached through
  `IAuditTrail` and `INotifier`, both through the service's outbox.
- The first administrator is seeded from `ADMIN_EMAIL` / `ADMIN_PASSWORD`, and that bootstrap closes once
  any administrator exists; this feature adds no second way to become one.
- Staff are few; searching people by a substring of their email or name without an index is acceptable
  at this size. No measurement was recorded.

## Out of scope

- An access token already issued lives out its minutes (15). A lock ends sessions at the next refresh,
  not mid-request. Accepted: the alternative is a lookup on every request in every service. (Later closed
  by specs/065, #112, without a per-request lookup.)
- Deleting accounts.
- Rules on who may **unlock** whom. At this merge any member of staff could unlock any locked account;
  specs/050 (#121) added them.
- Telling a locked or banned person by notification or email; they learn at sign-in. Later work
  (specs/059, specs/083) added both.
- Closing a shop or removing `Seller`. Locking a seller stops the person, not their listings.
- Wording the sign-in refusal in the reader's language and time zone; the server's sentence is English and
  in UTC. Specs/049 (#120) changed that.
