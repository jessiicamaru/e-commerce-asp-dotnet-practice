# Feature Specification: Audit gaps and the misleading reuse warning

**Feature Branch**: `058-audit-gaps` | **Created**: 2026-09-24 | **Issue**: #128 (part A of two)

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

## User Scenarios

### US1 - Every write that matters is on the record (P1)

**Acceptance**
1. Setting a category translation records `CategoryTranslated`, and removing one records
   `CategoryTranslationRemoved`, under Catalog.
2. Removing a product translation records `ProductTranslationRemoved`.
3. Changing the default address records `DefaultAddressChanged` under User. Like every address entry,
   it names no address content.
4. Signing out records `SignedOut` under Security, with the person as the actor. Signing out with no
   token, or an unknown one, records nothing, since nothing happened.
5. Detected reuse records `SessionReuseDetected` under Security. It is a security event worth keeping,
   not just a log line.

### US2 - A session ended by a stop is not called theft (P1)

**Acceptance**
1. Reuse is a **rotated** token (`ReplacedByToken` set) presented again after the grace window. Only
   that revokes every session.
2. A token revoked without rotation (by a lock, a ban or an earlier reuse sweep) presented again gets the
   same 401. It is logged at Information and ends nothing else.
3. A person locked, then unlocked, who signs in again, keeps the new session when a stale tab presents
   the old token.

## Decision

**A routine refresh is not audited.** Every open tab refreshes every few minutes, and recording each
refresh would bury the entries that matter. A session is on the record where it starts (`SignedIn`),
where it ends (`SignedOut`), and when it is abused (`SessionReuseDetected`).
