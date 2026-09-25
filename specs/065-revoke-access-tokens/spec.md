# Feature Specification: A stopped account's access token stops working within seconds

**Feature Branch**: `065-revoke-access-tokens` | **Created**: 2026-09-25 | **Issue**: #112

## Why

A lock, a ban or a password change ends a person's refresh tokens, but an access token already issued
keeps working until it expires, up to 15 minutes. For those minutes a banned person can still place
orders, post reviews and change listings. A revoked moderator keeps moderating, and whoever stole the
token that a password change was meant to stop keeps using it.

## User Scenarios

### US1 - Tokens issued before a stop are refused everywhere (P1)

**Acceptance**
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

### US2 - Nothing grows or leaks (P2)

**Acceptance**
1. An entry is forgotten once every token it could refuse has expired (an hour). Memory stays bounded.
2. Every instance gets every revocation: the queue is per instance, not per service.
3. A service restarted within the hour forgets the revocations it held. That is recorded as the known
   limit: at worst the old behaviour, a token living out its 15 minutes.

## Out of scope

- Shorter access tokens.
- A persisted revocation list.
