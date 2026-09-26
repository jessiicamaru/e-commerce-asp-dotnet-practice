# Phase 1 Data Model: A stopped account's access token stops working within seconds

> Written on 2026-09-27, after the feature merged (#148), from the code at that merge, the pull request and
> [docs/features/auth/jwt-setup.md](../../docs/features/auth/jwt-setup.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed, in any service.** Identity already had its
transactional outbox (it publishes seller and audit events), so the new event needed no outbox tables; the six
changes that publish it write the columns they wrote before. The revocation list is in memory.

---

## `RevokedAccessTokens` (in memory, one per service instance)

A singleton registered by `AddJwtAuthentication`, in `Ecommerce.Shared/Authentication/RevokedAccessTokens.cs`.

| Part | Shape | Meaning |
| :--- | :--- | :--- |
| `_revokedAt` | `ConcurrentDictionary<Guid, DateTime>` | user id → the latest revocation instant (UTC) |
| `Retention` | `TimeSpan.FromHours(1)` | after this, every token a revocation could refuse has expired (tokens live 15 minutes) |
| `Revoke(userId, at)` | `AddOrUpdate`, keeping the later instant; then prune entries older than now - 1 h | recorded by the consumer |
| `IsRevoked(userId, issuedAt)` | `issuedAt < at` truncated to its whole second | asked by `OnTokenValidated` |
| `IsRevoked(principal)` | reads `sub` (Guid) and `iat` (Unix seconds); false when either is missing | |

**Lifetime**: from the first revocation after the process starts until an hour after the latest one for that user,
or until the process stops. A restart empties it (the known limit).

**Bounds**: at most one entry per user revoked in the last hour.

## Queues (RabbitMQ, not a database, but state all the same)

One per running instance of each service that validates tokens:

| Queue | Durable | Expires | Consumer |
| :--- | :--- | :--- | :--- |
| `<svc>-access-revoked-<12 hex>` for `identity`, `catalog`, `cart`, `order`, `inventory`, `payment`, `activity` | no (`Temporary = true`) | `x-expires` 60000 ms after the instance disconnects (observed) | `AccessTokensRevokedConsumer` |
