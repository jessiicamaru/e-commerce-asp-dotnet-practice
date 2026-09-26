# Feature Specification: Audit gaps and the misleading reuse warning

> Completed on 2026-09-27, after the feature merged (#141), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Feature Branch**: `058-audit-gaps` | **Created**: 2026-09-24 | **Issue**: #128 (part A of two)

**Status**: Merged (#141, 2026-09-24)

**Input**: Issue #128, part A: "Some writes record no audit entry" - found while the documents for the audit log
(specs/041) were being written - together with the refresh-token reuse warning that fired for a session a lock
had ended.

## Why

Specs/041 says everything that matters is on the record. When the documents were written, several writes
turned out not to be:

- setting or removing a **category translation**;
- removing a **product translation** (only `ProductSentForReview` was recorded, and only when it
  applied);
- changing the **default delivery address**;
- **signing out**.

Translating a variant option was on that list too; specs/056 closed it.

Separately, a refresh token revoked by a **lock or ban** was later treated as **reuse**. It was logged as
"Refresh token reuse", and every session of that person was revoked. So after an unlock, one stale tab
from before the lock ended the session the person had just signed in with.

Part B, the missing notifications, follows in its own change.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every write that matters is on the record (Priority: P1)

An administrator reading the audit log can find every change to the catalogue's words, to a person's default
delivery address and to their sessions - not only the ones some handler happened to record.

**Why this priority**: An audit log with holes is worse than a short one: it is read as complete, so a change
missing from it reads as a change that never happened. Every entry here is a write that already reaches the
database; only the record of it was missing.

**Independent Test**: Translate a category into English and remove it, remove a product's English text, choose
another default address and sign out; `GET /api/audit` returns one entry for each, in the right category, and
none of them carries the address text or a token.

**Acceptance** (as first written)
1. Setting a category translation records `CategoryTranslated`, and removing one records
   `CategoryTranslationRemoved`, under Catalog.
2. Removing a product translation records `ProductTranslationRemoved`.
3. Changing the default address records `DefaultAddressChanged` under User. Like every address entry,
   it names no address content.
4. Signing out records `SignedOut` under Security, with the person as the actor. Signing out with no
   token, or an unknown one, records nothing, since nothing happened.
5. Detected reuse records `SessionReuseDetected` under Security. It is a security event worth keeping,
   not just a log line.

**Acceptance Scenarios**:

1. **Given** a category with no English text, **When** an administrator sets its English name, **Then** one
   `CategoryTranslated` entry under Catalog is recorded with the text before (none) and after.
2. **Given** a category with English text, **When** an administrator removes it, **Then** one
   `CategoryTranslationRemoved` entry is recorded whose "before" holds the removed name and description.
3. **Given** a product with English text, **When** its seller or an administrator removes it, **Then** one
   `ProductTranslationRemoved` entry under Catalog is recorded with the removed text as "before", alongside any
   `ProductSentForReview` the edit causes.
4. **Given** a customer with two addresses, **When** they make the second the default, **Then** one
   `DefaultAddressChanged` entry under User is recorded, and neither its summary nor its snapshots contain any
   part of the address.
5. **Given** a customer whose chosen address is already the default, **When** they choose it again, **Then**
   nothing is recorded, because nothing changed.
6. **Given** a signed-in session, **When** the person signs out, **Then** one `SignedOut` entry under Security is
   recorded with the person as the actor, and the refresh token appears nowhere in it.
7. **Given** no cookie or a token that names no session, **When** sign-out is called, **Then** nothing is
   recorded.

---

### User Story 2 - A session ended by a stop is not called theft (Priority: P1)

A person whose account was locked and then unlocked signs in again and keeps that session, even though a tab left
open from before the lock still presents its old refresh token.

**Why this priority**: The old rule did harm, not just noise. Every lock that was later lifted left behind tabs
that would each, at their next refresh, end every session of the person - including the one they had just
signed in with - and log it as theft.

**Independent Test**: Sign in, lock and unlock the account as an administrator, sign in again, then present the
first session's refresh token: it gets 401, and the second session still refreshes.

**Acceptance** (as first written)
1. Reuse is a **rotated** token (`ReplacedByToken` set) presented again after the grace window. Only
   that revokes every session.
2. A token revoked without rotation (by a lock, a ban or an earlier reuse sweep) presented again gets the
   same 401. It is logged at Information and ends nothing else.
3. A person locked, then unlocked, who signs in again, keeps the new session when a stale tab presents
   the old token.

**Acceptance Scenarios**:

1. **Given** a refresh token that was rotated more than the grace window ago, **When** it is presented again,
   **Then** the caller gets 401, a `SessionReuseDetected` entry is recorded, every active session of the person
   is revoked, and a warning is logged with the user id and no token.
2. **Given** a refresh token revoked by a lock (never rotated), **When** it is presented after the unlock,
   **Then** the caller gets the same 401, an Information line is logged, and no other session is touched.
3. **Given** a refresh token rotated within the grace window (two tabs refreshing together), **When** it is
   presented again, **Then** the caller gets 401 and nothing else happens, as before.

---

### Edge Cases

- **Sign-out with no access token.** Sign-out is called with only the HttpOnly cookie, and the access token may
  have expired. The actor is taken from the session's own row, as sign-in does, not from `ICurrentUser`.
- **Sign-out twice.** The second call finds no session and records nothing; sign-out never fails in a way the
  person has to deal with.
- **A default address chosen again.** Nothing changes and nothing is recorded.
- **Reuse and the audit entry.** The entry is on the record before the sessions end, so a revocation is never
  unexplained.
- **A token revoked by an earlier reuse sweep.** It was not rotated either, so presenting it again is refused
  quietly rather than starting another sweep.
- **The 401 is the same in every case**, so a caller learns nothing about which case it hit.
- **A routine refresh.** Not recorded (see Decision).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Setting a category's text in one language MUST record `CategoryTranslated` (Catalog) with the text
  before and after.
- **FR-002**: Removing a category's text in one language MUST record `CategoryTranslationRemoved` (Catalog) with
  the removed text as "before".
- **FR-003**: Removing a product's text in one language MUST record `ProductTranslationRemoved` (Catalog) with the
  removed text as "before".
- **FR-004**: Choosing another default delivery address MUST record `DefaultAddressChanged` (User), and the entry
  MUST NOT contain any part of the address.
- **FR-005**: Signing out MUST record `SignedOut` (Security) with the person as the actor, taken from the session
  row; a sign-out that ends no session MUST record nothing.
- **FR-006**: Every entry for a write MUST be committed with the change it describes, or not at all. The reuse
  entry (FR-008) is the one exception: it is saved on its own immediately before the revocation statement, so
  the record exists even if the revocation then fails.
- **FR-007**: Refresh-token reuse MUST mean a rotated token presented again after the grace window; only reuse
  revokes every session of the person.
- **FR-008**: Detected reuse MUST record `SessionReuseDetected` (Security) before the sessions are revoked.
- **FR-009**: A revoked token that was never rotated MUST be refused with the same 401 as reuse, logged at
  Information, and MUST NOT revoke any other session.
- **FR-010**: A routine refresh MUST NOT be recorded.

### Key Entities

- **Audit entry** (`AuditEntryRecorded`, kept by Activity in `audit_entries`): category, action, actor, subject,
  summary and redacted before/after snapshots. Unchanged by this feature; five new action names use it.
- **Refresh token** (`refresh_tokens` in Identity): `RevokedAt` and `ReplacedByToken` together now decide whether
  a presented, revoked token is reuse or a stale tab. No column was added.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Each of the five writes named in US1 produces exactly one audit entry, asserted by a test that failed
  before the change.
- **SC-002**: No `DefaultAddressChanged` or `SignedOut` entry contains the address text or the refresh token,
  asserted by the same tests.
- **SC-003**: After lock, unlock and a new sign-in, a stale tab's token leaves the new session able to refresh,
  asserted by `RefreshTokenReuseTests`.
- **SC-004**: The whole Identity and Catalog suites pass against real PostgreSQL (83/83 and 161/161 at the merge).

## Assumptions

- The audit machinery from specs/041 (`IAuditTrail`, the outbox, Activity's idempotent insert) is correct; this
  feature only calls it from more places.
- The reuse grace window stays at 10 seconds (`RefreshTokenCommandHandler.ReuseGrace`).
- A lock, a ban and a reuse sweep revoke tokens with a bulk statement that never sets `ReplacedByToken`, so the
  column tells rotation apart from the rest.

## Out of Scope

- The missing notifications (part B of #128) - [specs/059](../059-missing-notices/).
- Translating a variant option, closed earlier by specs/056.
- Ending a signed-in session's **access** token on a lock; that is specs/065.

## Decision

**A routine refresh is not audited.** Every open tab refreshes every few minutes, and recording each
refresh would bury the entries that matter. A session is on the record where it starts (`SignedIn`),
where it ends (`SignedOut`), and when it is abused (`SessionReuseDetected`).
