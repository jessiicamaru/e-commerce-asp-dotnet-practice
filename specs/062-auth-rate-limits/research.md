# Phase 0 Research: Limits on the sign-in and email endpoints

> Written on 2026-09-27, after the feature merged (#145), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-25

Seven decisions. D1 to D4 are the plan's numbered "Decisions"; D5 and D6 are in the pull request; D7 is
reconstructed from the code. The project-level record is decision 46 in
[docs/project/decisions.md](../../docs/project/decisions.md).

---

## D1 - Per email, not per account, and not a lock

**Decision**: Count wrong passwords per `EmailKey.For(email)` in `sign_in_throttles`; after 5 in 15 minutes, pause
every sign-in for that email for 5 minutes. It is not the moderation lock of specs/043.

**Rationale**: An account-row counter would answer differently for unknown emails (#28) - a test shows an unknown
address trips at the same point. A moderation lock would hand anybody a way to shut an account for 30 minutes or
more. The known limit, recorded in security §5: somebody who knows an email can keep its sign-in delayed with 5
wrong passwords every 5 minutes. They cannot get in, and the per-IP limit caps how many emails one client can do
that to.

**Alternatives considered**:

- **Counter on the user row.** Rejected for #28.
- **Lock the account automatically.** Rejected: a denial of service anybody could trigger, for as long as a lock
  lasts.
- **Per-IP only.** Rejected: many addresses guess one account without limit.

---

## D2 - In-memory counters at the gateway, database counters in Identity

**Decision**: The gateway's fixed windows live in the rate limiter's memory; the per-email count lives in
PostgreSQL.

**Rationale**: The gateway's limits are about one client's rate, and a restart forgiving them costs nothing. The
per-email count is about one account under attack from anywhere, so it must survive restarts and be shared by
every Identity instance.

**Alternatives considered**: a shared store (Redis) for the gateway - rejected as new infrastructure for a limit
whose loss costs nothing; the per-email count in memory - rejected because several Identity instances would each
allow five.

---

## D3 - `X-Forwarded-For` only from configured proxies, and only its last hop

**Decision**: `AddTrustedProxies` reads `GATEWAY_TRUSTED_PROXIES` (IPs or CIDRs, comma-separated), clears the
defaults, trusts only those, and sets `ForwardLimit = 1`. With nothing configured it sets `ForwardedHeaders.None`.
Compose puts the storefront at `172.30.10.10` on a new `edge` network (`172.30.10.0/24`) and trusts exactly that.

**Rationale**: Trusting the header from anyone makes every limit optional: the caller writes a new address on each
request. Without trusting the storefront, every browser behind it would share nginx's address and one person's
attempts would use up everybody's.

**Found while testing**: with `KnownProxies` **and** `KnownIPNetworks` both empty, ASP.NET Core's forwarded-headers
middleware trusts **every** peer, not none. A gateway with no trusted proxy believed a forged header, and a new
one per request bypassed the limit. `A_forwarded_address_from_an_untrusted_peer_is_ignored` caught it; the fix is
`ForwardedHeaders.None` when nothing is configured.

**Alternatives considered**: read the header always - rejected above; never read it - rejected: behind the
storefront every browser would share one allowance.

---

## D4 - A new test project for the gateway

**Decision**: `server/tests/Ecommerce.ApiGateway.Tests`, WebApplicationFactory over the real `Program`, every
destination unreachable. 502 means the request was let through; 429 means it was refused before reaching
anything. No database.

**Rationale**: The limits are configuration plus middleware order, and only a request through the real pipeline
proves them. Unit-testing `AuthRateLimits` alone would not catch a route without its policy or
`UseForwardedHeaders` in the wrong place - both mutations were caught this way.

**Alternatives considered**: only Bruno and the end-to-end run - rejected: neither can forge a peer address or
change configuration per case.

---

## D5 - The right password waits too, and the pause is asked first

**Decision**: `LoginCommandHandler` asks `BlockedUntilAsync` before looking up the account and before checking the
password. The attempt that starts the pause still gets the ordinary 401.

**Rationale**: If the right password got through a pause, the pause would still answer "right" or "wrong" to a
guesser. Asking before the account keeps an unknown email identical to a real one.

**Alternatives considered**: let the right password through - rejected for the oracle above.

---

## D6 - One statement per failure; the count goes back to zero when a pause starts

**Decision**: `RecordFailureAsync` is a single `INSERT ... ON CONFLICT ("EmailKey") DO UPDATE ... RETURNING
"Failures", "BlockedUntil"`. It restarts the window when it is older than 15 minutes, otherwise adds one; reaching
5 sets `BlockedUntil = now + 5 min` and `Failures = 0`. The call whose returned row has `Failures = 0` and
`BlockedUntil` equal to the instant it wrote is the one that started the pause - and records `SignInThrottled`,
only for a real account. `now` is truncated to PostgreSQL's microsecond precision first so that comparison holds.

**Rationale**: Simultaneous wrong passwords are all counted, and exactly one starts the pause and its audit entry.
Resetting to 0 means that after a pause another full run of five is needed. A successful sign-in or a reset
deletes the row.

**Alternatives considered**: read-modify-write - rejected: concurrent failures would be lost; an audit entry per
refused attempt while paused - rejected: it would flood the log.

---

## D7 - One reset email a minute per address, decided under a row lock

**Decision**: Inside the forgot-password transaction, `AskedSinceAsync` locks the user's row (`SELECT ... FOR
UPDATE`) and asks whether a token was created in the last minute (`ResetTokens.MinimumInterval`); if so the
handler returns and the answer is the same 202. Stale `sign_in_throttles` rows are purged hourly by
`SignInThrottleSweeper` (`SignIn:PurgeMinutes`, 60).

**Rationale**: The per-IP limit does not stop many clients filling one inbox. Locking the row makes two
simultaneous requests decide one after the other, so exactly one email is queued (a mutation without the lock was
caught). Purging keeps sprayed addresses from accumulating.

**Alternatives considered**: a configurable interval - the plan named `PasswordReset:MinimumIntervalSeconds`, but
the code uses a constant (corrected in [plan.md](plan.md)); counting in `sign_in_throttles` too - rejected: the
existing token row already says when a link was last asked for.
