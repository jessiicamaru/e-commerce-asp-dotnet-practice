# Implementation Plan: A stopped account's access token stops working within seconds

**Branch**: `065-revoke-access-tokens` | **Spec**: [spec.md](spec.md) | **Issue**: #112

## Design

- **The message:** `Contracts/Identity/AccessTokensRevoked.cs`, `(Guid UserId, DateTime RevokedAt,
  string Reason)`.
- **`Ecommerce.Shared/Authentication/RevokedAccessTokens`** is a singleton holding
  `ConcurrentDictionary<Guid, DateTime>`:
  - `Revoke(userId, at)` keeps the latest instant per user, and prunes entries older than the retention;
  - `IsRevoked(userId, issuedAt)` is true when `issuedAt < floor(RevokedAt, seconds)`.
- **`AddJwtAuthentication`** registers the singleton and adds `JwtBearerEvents.OnTokenValidated`. It reads
  `sub` and `iat` from the principal, and calls `Fail` when the token is revoked, which answers 401.
- **`AccessTokensRevokedConsumer`** (Shared) records the revocation. Each service registers it with
  `x.AddAccessTokenRevocations("catalog")`: an endpoint `catalog-access-revoked-{instance}`, **temporary**,
  so every instance of a service gets every message.
- **Identity publishes** through `IPublishEndpoint` before the save that makes the change:
  - lock, ban and role revoke (`UserAdministration`);
  - password reset (`PasswordResetHandlers`);
  - password change (`AccountHandlers`);
  - reuse detected (`RefreshTokenCommandHandler`).

## Decisions

1. **A revocation list, not shorter tokens.** Five-minute tokens triple the refresh traffic and still
   leave five minutes. The list closes the gap in seconds, and costs one small dictionary per service.
2. **The event means "tokens issued before now", not "this account is stopped".** One rule then covers
   a ban, where the refresh fails and the person is signed out, and a password change or role revoke, where
   the refresh succeeds with the new state.
3. **In memory, per instance, temporary queue.** A restart forgets, and the worst case is the old
   behaviour. A durable per-service queue would deliver each message to one instance only.

## Constitution check

- **I. Service Autonomy.** No synchronous call: each service keeps its own read model of revocations, fed
  by events. That is the codebase's usual answer, acceptable here because a stale copy fails **open to
  the old 15-minute behaviour**, never closed. Pass.
- **III.** Published through Identity's outbox with the change. Pass.
- **V.** Unit tests of the rule, a consumer test, publish tests for all six actions, mutation checks, and
  an end-to-end ban across services. Pass.
