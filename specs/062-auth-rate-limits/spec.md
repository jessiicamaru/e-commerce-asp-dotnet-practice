# Feature Specification: Limits on the sign-in and email endpoints

> Completed on 2026-09-27, after the feature merged (#145), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature Branch**: `062-auth-rate-limits` | **Created**: 2026-09-25 | **Issue**: #105

**Status**: Merged (#145, 2026-09-25). Later: specs/064 counts a wrong *current* password toward the same pause;
#178 added a limit on the product-view endpoint with the same machinery.

**Input**: Issue #105: nothing limits how fast anybody can guess a password or ask for reset emails.

## Why

Anybody can try passwords against an account as fast as they can send requests, and nothing slows them
down. Since specs/061 there is also an anonymous endpoint that sends a real email on every call:
somebody can fill a stranger's inbox with reset links, and fill `outgoing_emails` with them. Twelve wrong
passwords in a row were all answered at once (#105), and there is no rate limiter anywhere in the code.

## User Scenarios & Testing *(mandatory)*

### US1 - One client cannot hammer the anonymous auth endpoints (Priority: P1)

A client that sends sign-ins, registrations or reset requests faster than a person could type is slowed
down at the gateway, whichever emails it tries.

**Why this priority**: It is the cheapest protection and covers every anonymous auth endpoint at once, including
registration and the reset email, before any request reaches Identity.

**Independent Test**: Send six `forgot-password` requests from one client within a minute: five 202, then 429
with `Retry-After`; a sign-in from the same client still gets through.

**Acceptance** (as first written)
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

**Acceptance Scenarios**:

1. **Given** one client, **When** it sends a sixth `forgot-password` within a minute, **Then** it gets 429
   `application/problem+json` with `Retry-After: N` and `"retryAfter": N`, and Identity never sees the request.
2. **Given** a client that has used up the `email` allowance, **When** it signs in, **Then** the sign-in is
   answered normally.
3. **Given** two clients, **When** one uses up an allowance, **Then** the other's is untouched.
4. **Given** a client with no trusted proxy configured, **When** it writes a new `X-Forwarded-For` on every
   request, **Then** it is still counted by its connection address and refused at the limit.
5. **Given** the storefront as a trusted proxy, **When** two browsers call through it, **Then** each is counted by
   the address the storefront forwarded, and only the last hop counts.
6. **Given** a `RateLimits` setting of 0 or less, **When** the gateway starts, **Then** it refuses to start.

---

### US2 - Guessing one account's password is slowed, from any number of IPs (Priority: P1)

Somebody guessing one person's password from many addresses still gets only a few tries.

**Why this priority**: The per-IP limit alone lets a guesser with many addresses try without end against one
account; the account is what is under attack.

**Independent Test**: Five wrong passwords for one email, then the right one: 401 five times, then 429 with
`Retry-After` near 300. The same for an address that has no account.

**Acceptance** (as first written)
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

**Acceptance Scenarios**:

1. **Given** an email, **When** five wrong passwords arrive within 15 minutes, **Then** each gets 401 (the fifth
   starts the pause) and the next sign-in, right password or not, gets 429 "Too many wrong passwords for this
   email. Try again later." with `Retry-After` up to 300.
2. **Given** the pause has ended, **When** another wrong password arrives, **Then** it is a 401 and a full run of
   five is needed before the next pause.
3. **Given** five wrong passwords sent at once, **When** they are counted, **Then** all five are counted and
   exactly one starts the pause (and one `SignInThrottled`).
4. **Given** an email typed in different capitals or with spaces, **When** wrong passwords arrive, **Then** they
   count against the same address (`EmailKey.For`).
5. **Given** wrong passwords older than the 15-minute window, **When** a new one arrives, **Then** the count starts
   again from 1.
6. **Given** a paused address with no account, **When** it trips, **Then** nothing is recorded in the audit log.

---

### US3 - An inbox cannot be flooded with reset links (Priority: P1)

**Why this priority**: Since specs/061 every call can put a real email in a stranger's inbox; the per-IP limit
alone does not stop many clients asking for one address.

**Independent Test**: Ask for a reset link for one real address twice within a minute, and many times at once:
every answer is 202 and exactly one email is queued.

**Acceptance** (as first written)
1. `forgot-password` sends at most **one email per address per minute**, however many IPs ask. A request
   inside that minute still gets the same 202 and sends nothing.

**Acceptance Scenarios**:

1. **Given** a link asked for a real address, **When** another is asked within a minute, **Then** 202 and no second
   email.
2. **Given** many simultaneous requests for one address, **When** they are handled, **Then** exactly one email is
   queued (the person's row is locked first).
3. **Given** a minute has passed, **When** a link is asked for again, **Then** a new link and email are made.

---

### US4 - A person is told to wait, in their language (Priority: P2)

**Why this priority**: The protection works without it, but a bare "request failed" would send a person to try
again, which only extends their wait.

**Independent Test**: With a mocked 429 carrying `retryAfter: 290`, the sign-in page says "Try again in 5
minutes"; in Vietnamese the same in Vietnamese.

**Acceptance** (as first written)
1. On 429, the sign-in, sign-up, forgot-password and reset-password pages say "Too many attempts. Try
   again in N minutes." in Vietnamese or English. N comes from the server's `retryAfter`, rounded up to
   whole minutes.

**Acceptance Scenarios**:

1. **Given** a 429 with `retryAfter` 30, **When** it is shown, **Then** N is 1 - never zero.
2. **Given** a 429 whose body has no `retryAfter` but a `Retry-After` header, **When** it is shown, **Then** the
   header is used.
3. **Given** a 429 with neither, **When** it is shown, **Then** the person is asked to wait without a number.

---

### Edge Cases

- **Two tabs sharing one refresh cookie** refresh together, hence the looser `session` allowance.
- **A gateway restart** forgets its counters - acceptable for limits about one client's pace.
- **Several gateway instances** each count separately (out of scope; a known limit).
- **Somebody who knows an email** can keep its sign-in paused with 5 wrong passwords every 5 minutes. They cannot
  get in, and the per-IP limit caps how many emails one client can do that to (recorded in security §5).
- **The attempt that starts the pause** still gets the ordinary 401; only later ones get 429.
- **Bruno's collection** must not be throttled by its own check: the check uses only the `email` allowance and runs
  last in `security-checks`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The gateway uses ASP.NET Core's built-in rate limiter, attached to the auth routes through
  YARP's per-route `RateLimiterPolicy`.
- **FR-002**: Identity refuses a throttled sign-in with a shared `TooManyRequestsException`, which the
  shared `GlobalExceptionHandler` turns into 429 with `Retry-After`.
- **FR-003**: Stale per-email rows are purged, so addresses somebody sprayed do not accumulate forever.
- **FR-004**: A Bruno check shows rapid reset requests reaching 429, and it cannot throttle the rest of the
  collection.
- **FR-005**: Defaults: `sign-in` 30, `email` 5, `session` 60 requests per 60 seconds per client IP; a missing value
  uses the default and a non-positive one refuses to start.
- **FR-006**: The gateway MUST NOT read `X-Forwarded-For` unless `GATEWAY_TRUSTED_PROXIES` names the peer, and then
  only the last hop.
- **FR-007**: The per-email pause (`SignIn:MaxFailures` 5, `WindowMinutes` 15, `CooldownMinutes` 5) MUST be asked
  before the account is looked up and before the password is checked.
- **FR-008**: `forgot-password` MUST queue at most one email per address per minute and answer 202 either way.
- **FR-009**: Every 429 MUST carry `Retry-After` and `retryAfter` in whole seconds, rounded up, never zero.

### Key Entities

- **Allowance** (gateway, in memory): a fixed window per policy per client IP.
- **Sign-in throttle** (`sign_in_throttles` in Identity): per email key, the failures in the current window, when
  the window started, and until when sign-in is paused.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The sixth reset request from one client in a minute is refused, and 100% of refused requests carry
  how long to wait.
- **SC-002**: After 5 wrong passwords, the right password is refused for up to 5 minutes, identically for a real
  and an unknown email.
- **SC-003**: However many clients ask, one address receives at most one reset email a minute.
- **SC-004**: A forged `X-Forwarded-For` never changes which allowance a request uses.
- **SC-005**: The full Bruno collection still passes with the new check in it.

## Assumptions

- Every storefront request reaches the gateway through the storefront's nginx at a fixed address in compose; in
  `start-dev` (Vite proxy) no proxy is trusted and the connection address is used.
- A single gateway instance at this stage.
- The limits' defaults leave room for `verify-auth.sh`, `verify-saga.sh` and Bruno (the full collection passed).

## Out of scope

- Telling a person by email that somebody is guessing their password.
- CAPTCHA.
- Distributed limits across several gateway instances. The gateway's counters are in memory, per
  instance (recorded as a known limit).
