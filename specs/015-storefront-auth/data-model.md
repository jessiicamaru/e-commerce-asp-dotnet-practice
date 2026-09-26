# Data Model: Sign Up, Sign In, Stay Signed In

> Written on 2026-09-27, after the feature merged (#44), from the code at that merge, the pull request
> and docs/architecture/storefront.md and docs/features/auth/security-best-practices.md.

**Feature**: [spec.md](./spec.md)

**No table changed and no migration was added.** Logout uses a table that already existed.

## `refresh_tokens` (Identity, `ecommerce_identity_db`) - schema unchanged

Mapped by `RefreshTokenConfiguration`, created in `20260831073201_InitialCreate`. One row per live
session, owned by a user (`User.RefreshTokens`), with an `ExpiresAt` 7 days after issue
(`JwtConstants.TokenDurationDay`) and a nullable `RevokedAt` the refresh handler checks.

What this feature does to it:

| Operation | Effect on the row |
| :--- | :--- |
| Register, login | insert one (as before) |
| Refresh | delete the presented one, insert a new one (as before) |
| **Logout (new)** | **delete the presented one**; nothing if it is missing, empty or unknown |

A deleted row is what makes the old cookie useless: the refresh handler looks the token up and answers
401 when it is not there.

## The cookie

`refreshToken`, `HttpOnly; Secure; SameSite=Lax; path=/`, expiring after 7 days; on logout it is
deleted with the same options (the browser receives an expiry in 1970).

## Client state (memory only)

| Name | Where | Holds |
| :--- | :--- | :--- |
| `accessToken` | module variable in `client/src/auth/AuthContext.tsx` | the bearer token, or `null` |
| `user` | React state in `AuthProvider` | `{ id, email, firstName, lastName }`, or `null` |
| `restoring` | React state | `true` until the first refresh on load has answered |
| `refreshing` | `useRef` | the one refresh promise in flight, shared by concurrent 401s |

Nothing is written to `localStorage`, `sessionStorage` or a script-readable cookie.
