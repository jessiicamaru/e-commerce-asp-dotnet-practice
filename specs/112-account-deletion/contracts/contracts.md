# Contracts: A person deletes their account

## HTTP

### `DELETE /api/auth/me` - signed in

```json
{ "password": "…" }
```

| Answer | When |
| :-- | :-- |
| 204 | Deleted. Every session is revoked. |
| 400 | `password` empty, or wrong (`errors.Password`). A wrong one counts toward the sign-in pause. |
| 401 | No token, or a revoked one. |
| 409 | `code: "StaffAccount"` - the caller holds Admin or Moderator. |
| 409 | `code: "AccountHasOpenBusiness"`, `reasons: ["OpenOrders", "OpenReturns", "OpenSales", "UnpaidEarnings"]` (those that apply). |
| 429 | The gateway's `sign-in` limit. |
| 503 | Order could not be asked. |

The gateway route `auth-me-delete-route` matches `DELETE /api/auth/me` only and applies the `sign-in` limiter.

## gRPC - `account_standing.proto` (Order serves, Identity calls)

```proto
service AccountStanding {
  // Empty on purpose: whose standing is decided by the forwarded bearer token, never by a field.
  rpc GetMyStanding (GetMyStandingRequest) returns (AccountStandingReply);
}
message GetMyStandingRequest {}
message AccountStandingReply { repeated string blockers = 1; }
```

`blockers` holds zero or more of `OpenOrders`, `OpenReturns`, `OpenSales`, `UnpaidEarnings`.

## Message - `Ecommerce.Contracts.Identity.AccountDeleted`

```csharp
public record AccountDeleted(Guid UserId, string Email, DateTime DeletedAt);
```

Published by Identity through its outbox in the deleting transaction, with `AccessTokensRevoked`. Consumed by
Catalog (`EraseAccountFromCatalogConsumer`), Order (`EraseAccountFromOrdersConsumer`), Cart
(`EraseAccountFromCartConsumer`) and Activity (`EraseAccountFromActivityConsumer`).

## Audit

`Security` / `AccountDeleted` on the `User`, summary "An account was deleted" - no email, no snapshot.
