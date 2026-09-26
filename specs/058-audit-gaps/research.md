# Phase 0 Research: Audit gaps and the misleading reuse warning

> Written on 2026-09-27, after the feature merged (#141), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-24

Six decisions. The first was recorded in the spec as "decided on the user's behalf"; the rest are reconstructed
from the code and the pull request. Who took them beyond that is not recorded.

---

## D1 - A routine refresh is not audited

**Decision**: Record a session where it starts (`SignedIn`, already there), where it ends (`SignedOut`, new) and
when it is abused (`SessionReuseDetected`, new). Do not record refreshes.

**Rationale**: Every open tab refreshes every few minutes. Entries for those would outnumber everything else in
the log and bury the ones that matter, and they say nothing a reader needs: a session that exists is already
known from its sign-in.

**Alternatives considered**:

- **Record every refresh.** Rejected for the noise above.
- **Record only a refresh that crosses some interval.** Rejected: a rule nobody reading the log would know,
  producing entries that still mean nothing.

---

## D2 - Reuse means a rotated token presented again after the grace window

**Decision**: In `RefreshTokenCommandHandler`, a revoked token is reuse only when `ReplacedByToken` is set **and**
more than `ReuseGrace` (10 seconds) has passed since `RevokedAt`. Only reuse revokes every session.

**Rationale**: Reuse detection exists because a rotated token presented again means two parties held it. A token
revoked **without** rotation - by a lock, a ban, or an earlier reuse sweep, all of which set only `RevokedAt` - was
never handed to anybody afterwards; presenting it again is a tab left open from before. The old rule
(`!(ReplacedByToken != null && within grace)`) called every such token reuse, so after an unlock one stale tab
ended the session the person had just signed in with (#128). That is harm, not a misleading log line.

**Alternatives considered**:

- **Keep the old rule and only soften the log message.** Rejected: the log was the lesser problem; the new
  session was being ended.
- **Delete tokens on a lock instead of revoking them**, so a stale tab finds no row. Rejected: the revoked rows
  are the evidence of what a lock did, and a missing row and a made-up token would then be indistinguishable in
  the log.
- **Add a "revocation reason" column.** Rejected as unnecessary: `ReplacedByToken` already distinguishes rotation
  from every other revocation, and a column would be a schema change for a fact the data already holds.

---

## D3 - The same 401 in every case

**Decision**: Reuse, a stale tab and a rotation within the grace window all end in the same
`UnauthorizedAccessException` with the same message. Only the server-side effects differ: a warning and a sweep
for reuse, an Information line for a stale tab, nothing for a concurrent refresh.

**Rationale**: The caller of a refresh endpoint may be the thief. Telling them which case they hit would tell them
whether the theft was noticed.

**Alternatives considered**: a distinct response for a stale tab (so the storefront could say "you were signed
out") - rejected for the reason above; the storefront already treats any failed refresh as signed out.

---

## D4 - The reuse entry is saved before the sessions end, on its own

**Decision**: Record `SessionReuseDetected` and call `SaveChangesAsync`, then run `RevokeAllRefreshTokensAsync`,
a separate bulk `ExecuteUpdate`.

**Rationale**: The entry describes an event (a replaced token came back), not the revocation. Putting it on the
record first means every mass revocation is explained. If the revocation then fails, the entry says more than
happened, and the next presentation of the token detects reuse again and repeats both.

**Alternatives considered**:

- **Save the entry in the same transaction as the revocation.** Not what was built. It would need an explicit
  transaction around a bulk statement that today runs on its own; the plan first said "saved with the
  revocation", which the code does not do (corrected in [plan.md](plan.md)).
- **A log line only, as before.** Rejected: a security event belongs where administrators look for them, not only
  in Seq.

---

## D5 - Sign-out's actor comes from the session's row

**Decision**: `LogoutCommandHandler` records with `actor: AuditActors.Of(user)`, the user found by the refresh
token, not the caller from `ICurrentUser`.

**Rationale**: Sign-out is called with the HttpOnly cookie; the access token may be missing or expired. Sign-in
has the same shape and already records its actor from the row. An entry whose actor is "the system" for a person
signing out would be wrong.

**Alternatives considered**: require a valid access token to sign out - rejected: signing out must never fail in
a way the person has to deal with (the handler's own remark), and an expired access token is the ordinary case.

---

## D6 - "Changed", never the address; and where the translation entry sits

**Decision**: `DefaultAddressChanged` carries no snapshot, only a summary without address text, recorded inside
the handler's transaction before the second save (the one that promotes the new default).
`ProductTranslationRemoved` is recorded before `ProductReview.AfterSellerEditAsync`, so the removal precedes any
`ProductSentForReview` it causes, and both commit in the one save.

**Rationale**: Every address entry since specs/041 says an address changed and never copies it; the audit log is
read by staff who have no business reading delivery addresses. The handler demotes the old default and saves,
then promotes, so the entry belongs with the promotion. Recording the removal first keeps cause before effect in
the log.

**Alternatives considered**: snapshot the address ids before and after - rejected as adding nothing a reader can
use without opening the address book, which the entry exists to avoid.
