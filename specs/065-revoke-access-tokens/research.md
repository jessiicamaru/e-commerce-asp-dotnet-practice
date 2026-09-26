# Phase 0 Research: A stopped account's access token stops working within seconds

> Written on 2026-09-27, after the feature merged (#148), from the code at that merge, the pull request and
> [docs/features/auth/jwt-setup.md](../../docs/features/auth/jwt-setup.md).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-25

Five decisions. D1 to D3 are the plan's "Decisions"; D4 and D5 are in the pull request. The project-level record is
decision 49 in [docs/project/decisions.md](../../docs/project/decisions.md).

---

## D1 - A revocation list, not shorter tokens

**Decision**: Keep 15-minute access tokens and refuse revoked ones from an in-memory list in every service.

**Rationale**: Five-minute tokens triple the refresh traffic and still leave five minutes. The list closes the gap
in seconds, and costs one small dictionary per service.

**Alternatives considered**:

- **Shorter access tokens.** Rejected above (out of scope in the spec).
- **Ask Identity on every request** (a lookup per request). Rejected: a synchronous dependency of every service on
  Identity for every call, and a new way for an Identity outage to take the whole shop down.
- **A persisted list in each service.** Rejected (out of scope): it would survive restarts, but a table per service
  for entries that matter for an hour is more than the one remaining limit is worth.

---

## D2 - The event means "tokens issued before now", not "this account is stopped"

**Decision**: `AccessTokensRevoked(UserId, RevokedAt, Reason)`; the rule is the same for every reason, which is kept
for logs.

**Rationale**: One rule then covers a ban, where the refresh fails and the person is signed out, and a password
change or role revoke, where the refresh succeeds with the new state. The storefront already refreshes on a 401.

**Alternatives considered**: an "account stopped" event carrying the stop's kind - rejected: every service would then
need to know which stops end sessions and which only change them, rules that belong to Identity alone.

---

## D3 - In memory, per instance, temporary queue

**Decision**: `AddAccessTokenRevocations("<svc>")` registers `AccessTokensRevokedConsumer` on an endpoint named
`<svc>-access-revoked-<12 hex>` with `Temporary = true` (non-durable, `x-expires` 60 s as observed in RabbitMQ).

**Rationale**: A restart forgets, and the worst case is the old behaviour. A durable per-service queue would deliver
each message to one instance only - the queue-collision trap from CLAUDE.md one level down.

**Alternatives considered**: one durable queue per service - rejected above; a fan-out to a named queue per instance
that survives restarts - rejected: an instance that never returns would leave a queue filling for ever.

---

## D4 - Whole seconds

**Decision**: `IsRevoked(userId, issuedAt)` is `issuedAt < WholeSecond(RevokedAt)`: a revocation counts from the
start of its second.

**Rationale**: `iat` has whole seconds. Comparing with the exact instant would refuse the new token of the session a
password change keeps - issued in the same second as the revocation - and that session would refresh for ever. A
test holds this, and a mutation proves the test catches it.

**Alternatives considered**: compare exact instants - rejected above; add a grace of a few seconds - rejected: it
would let a token issued just before the stop through for no reason.

---

## D5 - Fails open; the latest revocation wins; forgotten after an hour

**Decision**: `Revoke` keeps the later instant per user (`AddOrUpdate` with a max) and prunes entries older than
`RevokedAccessTokens.Retention` (one hour) on every call; a principal without a parsable `sub` or `iat` is never
refused by this check.

**Rationale**: After an hour every token a revocation could refuse has expired, so memory stays bounded. Messages of
two types have no order between them, so an older revocation arriving late must not shorten a newer one. Nothing in
the list can refuse a token Identity would accept after a refresh - the stale case is always the old behaviour.

**Alternatives considered**: a timer to prune - rejected: pruning on write is enough, since the list only grows when
written.
