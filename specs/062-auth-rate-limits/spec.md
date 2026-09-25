# Feature Specification: Limits on the sign-in and email endpoints

**Feature Branch**: `062-auth-rate-limits` | **Created**: 2026-09-25 | **Issue**: #105

## Why

Anybody can try passwords against an account as fast as they can send requests, and nothing slows them
down. Since specs/061 there is also an anonymous endpoint that sends a real email on every call:
somebody can fill a stranger's inbox with reset links, and fill `outgoing_emails` with them. Twelve wrong
passwords in a row were all answered at once (#105), and there is no rate limiter anywhere in the code.

## User Scenarios

### US1 - One client cannot hammer the anonymous auth endpoints (P1)

A client that sends sign-ins, registrations or reset requests faster than a person could type is slowed
down at the gateway, whichever emails it tries.

**Acceptance**
1. Per client IP, in a fixed window: sign-in, registration (both kinds) and choosing a new password share
   a **sign-in** limit; asking for a reset link has its own, tighter **email** limit; refreshing a session
   has a looser **session** limit, because two tabs share one cookie and refresh together.
2. A request over a limit gets **429** as ProblemDetails, with `Retry-After` in seconds and the same number
   as `retryAfter` in the body. It never reaches Identity.
3. Using up one limit does not touch another: a client that asked for reset links too fast can still sign
   in.
4. Nothing else behind the gateway is limited.
5. The client IP comes from the connection. `X-Forwarded-For` is believed **only** from a proxy the
   gateway is configured to trust (the storefront's nginx in compose); from anyone else it is ignored,
   because anybody can write that header.
6. The limits are configuration, and the defaults leave room for Bruno, `verify-auth.sh` and
   `verify-saga.sh`.

### US2 - Guessing one account's password is slowed, from any number of IPs (P1)

Somebody guessing one person's password from many addresses still gets only a few tries.

**Acceptance**
1. After **5** wrong passwords for one email within **15 minutes**, every sign-in for that email is
   answered **429** with `Retry-After` for **5 minutes**, even with the right password.
2. The count is kept per **email**, not per account, so an address with no account behaves exactly like
   one with an account (#28): the same 401s, the same 429 at the same point.
3. The count lives in the database and is changed by single atomic statements, so several Identity
   instances agree and simultaneous wrong passwords are all counted.
4. A successful sign-in, or a password reset, clears the count.
5. Tripping it for a real account is a **Security** audit entry, `SignInThrottled`.
6. It is a short delay, **not** the moderation lock from specs/043: an automatic lock would let anybody
   lock anybody out for as long as a moderator's lock lasts.

### US3 - An inbox cannot be flooded with reset links (P1)

**Acceptance**
1. `forgot-password` sends at most **one email per address per minute**, however many IPs ask. A request
   inside that minute still gets the same 202 and sends nothing.

### US4 - A person is told to wait, in their language (P2)

**Acceptance**
1. On 429, the sign-in, sign-up, forgot-password and reset-password pages say "Too many attempts. Try
   again in N minutes." in Vietnamese or English. N comes from the server's `retryAfter`, rounded up to
   whole minutes.

## Requirements

- **FR-001**: The gateway uses ASP.NET Core's built-in rate limiter, attached to the auth routes through
  YARP's per-route `RateLimiterPolicy`.
- **FR-002**: Identity refuses a throttled sign-in with a shared `TooManyRequestsException`, which the
  shared `GlobalExceptionHandler` turns into 429 with `Retry-After`.
- **FR-003**: Stale per-email rows are purged, so addresses somebody sprayed do not accumulate forever.
- **FR-004**: A Bruno check shows rapid reset requests reaching 429, and it cannot throttle the rest of the
  collection.

## Out of scope

- Telling a person by email that somebody is guessing their password.
- CAPTCHA.
- Distributed limits across several gateway instances. The gateway's counters are in memory, per
  instance (recorded as a known limit).
