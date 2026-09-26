# Message Contracts: Access tokens revoked

> Written on 2026-09-27, after the feature merged (#148), from the code at that merge, the pull request and
> [docs/features/auth/jwt-setup.md](../../../docs/features/auth/jwt-setup.md).

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

---

## `AccessTokensRevoked` - `Ecommerce.Contracts.Identity` (new)

```csharp
record AccessTokensRevoked(Guid UserId, DateTime RevokedAt, string Reason);
```

Every access token of `UserId` issued before `RevokedAt` (to its whole second) stops working. It says "tokens issued
before now", not "this account is stopped". (The record's own doc comment says "specs/063 #112"; the feature is
specs/065.)

| Field | Meaning |
| :--- | :--- |
| `UserId` | Whose tokens |
| `RevokedAt` | UTC instant; tokens with an earlier `iat` second are refused |
| `Reason` | `Locked`, `Banned`, `RoleRevoked`, `PasswordReset`, `PasswordChanged`, `SessionReuseDetected` - for logs; the rule is the same for all |

### Publisher: Identity, through its transactional outbox

Staged with `IPublishEndpoint.Publish` before the save of the change, so it commits with it or not at all:

| Reason | Where | `RevokedAt` |
| :--- | :--- | :--- |
| `Locked` | `UserAdministrationHandlers` (lock) | now |
| `Banned` | `UserAdministrationHandlers` (ban) | now |
| `RoleRevoked` | `UserAdministrationHandlers` (revoke role) | `user.UpdatedAt` |
| `PasswordReset` | `PasswordResetHandlers` (reset, inside its transaction) | now |
| `PasswordChanged` | `AccountHandlers` (change, inside its transaction) | now |
| `SessionReuseDetected` | `RefreshTokenCommandHandler` (reuse) | now |

Granting a role publishes nothing.

### Consumers: every service that validates tokens

`AccessTokensRevokedConsumer` (`Ecommerce.Shared.Authentication`), registered with
`x.AddAccessTokenRevocations("<svc>")` in the `Program.cs` of Identity, Catalog, Cart, Order, Inventory, Payment and
Activity. It records the revocation in the instance's `RevokedAccessTokens` and logs it at Information.

**Delivery**: a **temporary queue per instance** (`<svc>-access-revoked-<id>`), so every instance of every service
gets every message. One durable queue per service would deliver each message to one instance only.

**Idempotency and order**: recording the same message twice changes nothing; the latest instant per user wins, so an
older revocation arriving late cannot overwrite a newer one. No database is involved.

**Loss**: a message missed (instance down, queue expired) leaves the old behaviour for that instance - a token lives
out its 15 minutes. The list fails open, never closed.
