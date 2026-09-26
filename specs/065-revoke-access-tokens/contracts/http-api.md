# HTTP Contract: Access tokens revoked

> Written on 2026-09-27, after the feature merged (#148), from the code at that merge, the pull request and
> [docs/features/auth/jwt-setup.md](../../../docs/features/auth/jwt-setup.md).

**Feature**: [spec.md](../spec.md)

No endpoint was added and no request or response shape changed. What changed is the answer every protected endpoint
of seven services gives to a revoked token.

## Every `[Authorize]` endpoint in Identity, Catalog, Cart, Order, Inventory, Payment and Activity

| Token | Before #148 | After |
| :--- | :--- | :--- |
| Valid, issued after any revocation of its user (or none in the last hour) | as before | as before |
| Valid signature and lifetime, `iat` second earlier than its user's latest revocation | accepted until it expired (up to 15 minutes) | **401** |

The refusal comes from `JwtBearerEvents.OnTokenValidated` calling `context.Fail("This token was issued before its
account's access was revoked.")`, so the response is the JWT handler's ordinary 401 challenge - the same as for an
expired token. Anonymous endpoints are unaffected.

## What the storefront does with it

It already refreshes on a 401 and retries:

| Case | `POST /api/auth/refresh` | Outcome |
| :--- | :--- | :--- |
| Lock, ban, reuse | refused (refresh tokens were revoked too) | signed out |
| Password change (this browser) | 200 (its cookie was kept) | carries on with a new token |
| Password reset, role revoked | 200 where a refresh token survived | carries on with the new state |

## Bruno

| Request | Asserts |
| :--- | :--- |
| `admin-insights/an administrator revokes moderator.yml` | the revoke, moved to the end of the last folder that needs the moderator's token |
| `admin-insights/the revoked moderator's token stops at once.yml` | after 1.5 s, `GET /api/shop-applications?status=Pending` with the old moderator token is 401 |
