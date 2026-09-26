# Feature Specification: A stopped account's access token stops working within seconds

> Completed on 2026-09-27, after the feature merged (#148), from the code at that merge, the pull request and
> [docs/features/auth/jwt-setup.md](../../docs/features/auth/jwt-setup.md) with
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature Branch**: `065-revoke-access-tokens` | **Created**: 2026-09-25 | **Issue**: #112

**Status**: Merged (#148, 2026-09-25).

**Input**: Issue #112: an access token outlives the lock, ban, role change or password change that should have
ended it.

## Why

A lock, a ban or a password change ends a person's refresh tokens, but an access token already issued
keeps working until it expires, up to 15 minutes. For those minutes a banned person can still place
orders, post reviews and change listings. A revoked moderator keeps moderating, and whoever stole the
token that a password change was meant to stop keeps using it.

## User Scenarios & Testing *(mandatory)*

### US1 - Tokens issued before a stop are refused everywhere (Priority: P1)

**Why this priority**: It is the whole feature. Every stop specs/043, 058, 061 and 064 built ends refresh tokens
only; until the access token is refused too, each leaves a window of up to 15 minutes.

**Independent Test**: Hold a customer's access token, ban the customer as an administrator, and call Identity,
Cart, Order, Activity, Catalog and Inventory with the old token: every one answers 401 within about two seconds.

**Acceptance** (as first written)
1. When Identity **locks** or **bans** an account, **revokes a role**, **resets** or **changes** its
   password, or detects a **reused refresh token**, it publishes `AccessTokensRevoked(UserId, RevokedAt,
   Reason)` through its outbox, in the transaction that made the change.
2. Every service that validates tokens (Identity, Catalog, Cart, Order, Inventory, Payment, Activity)
   keeps these in memory. It answers **401** to any token of that user **issued before** `RevokedAt`,
   within seconds, on every instance.
3. A token issued afterwards is accepted. That is the seamless path: after a password change or a role
   revocation the refresh token still works, the storefront refreshes on the 401, and the new token
   carries the new state. After a lock or ban the refresh itself is refused, so the person is signed out.
4. Tokens carry `iat` in whole seconds, so a revocation counts from the start of its second. A token
   issued in the same second as the revocation is accepted; otherwise the session a password change
   keeps would refuse its own new token.

**Acceptance Scenarios**:

1. **Given** a customer's access token, **When** an administrator bans them, **Then** each of the seven services
   answers 401 to that token within seconds, and the customer's refresh is refused too.
2. **Given** a signed-in person, **When** they change their password in this browser, **Then** the old access token
   gets 401, the browser refreshes with its kept cookie, and the new token gets 200.
3. **Given** a moderator's token, **When** an administrator revokes the role, **Then** the old token gets 401 on a
   staff endpoint, and the next refresh yields a token without the role.
4. **Given** a token issued in the same second as the revocation, **When** it is presented, **Then** it is accepted.
5. **Given** a role **granted**, **When** it happens, **Then** nothing is revoked (the role arrives at the next
   refresh, as before).

---

### US2 - Nothing grows or leaks (Priority: P2)

**Why this priority**: A list kept in every service's memory must not grow without end, and must reach every
instance - or it protects only some requests.

**Independent Test**: The unit tests show an entry forgotten after an hour and the latest revocation winning;
`rabbitmqctl list_queues` shows one temporary `<svc>-access-revoked-<id>` queue per running instance.

**Acceptance** (as first written)
1. An entry is forgotten once every token it could refuse has expired (an hour). Memory stays bounded.
2. Every instance gets every revocation: the queue is per instance, not per service.
3. A service restarted within the hour forgets the revocations it held. That is recorded as the known
   limit: at worst the old behaviour, a token living out its 15 minutes.

**Acceptance Scenarios**:

1. **Given** a revocation older than an hour, **When** another revocation arrives, **Then** the old entry is pruned.
2. **Given** two revocations for one user arriving out of order, **When** both are recorded, **Then** the later
   instant wins.
3. **Given** two instances of one service, **When** Identity publishes a revocation, **Then** both record it.

---

### Edge Cases

- **A token without `sub` or `iat`** is never refused by this check (it is not a token Identity issued this way).
- **A message delayed or lost** leaves the old 15-minute behaviour for that token: the list fails open, never
  closed.
- **Identity itself** validates tokens too, so it registers the consumer like every other service.
- **The gateway and the orchestrator** validate no tokens and register nothing.
- **Bruno's own moderator** stopped working the moment its role was revoked - the change working - so the revoke
  moved to the end of the last folder that needs the token.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Identity MUST publish `AccessTokensRevoked` on a lock, a ban, a role revoked, a password reset, a
  password change and a detected refresh-token reuse, staged before the save of that change; a grant MUST publish
  nothing.
- **FR-002**: Every service that validates tokens MUST refuse, with 401, a token whose `sub` has a revocation and
  whose `iat` is earlier than the revocation's whole second.
- **FR-003**: Every instance of every such service MUST receive every revocation.
- **FR-004**: An entry MUST be forgotten after one hour; the latest revocation per user MUST win.
- **FR-005**: The check MUST fail open: no stored state can refuse a token Identity would still accept after a
  refresh.

### Key Entities

- **Revocation** (`AccessTokensRevoked`): a user, an instant, a reason (for logs only).
- **Revocation list** (`RevokedAccessTokens`, per instance, in memory): the latest instant per user.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a ban, a token issued before it is refused by every service within seconds (1758 ms across six
  services in the end-to-end run).
- **SC-002**: A password change never signs out the browser that made it.
- **SC-003**: Memory per service holds at most an hour of revocations.
- **SC-004**: The whole server suite passes (635/635 in 9 projects at the merge).

## Assumptions

- Access tokens live 15 minutes and carry `sub` and `iat` (seconds).
- The storefront refreshes on a 401 and retries.
- RabbitMQ delivers to temporary queues while the instance is connected.

## Out of scope

- Shorter access tokens.
- A persisted revocation list.
