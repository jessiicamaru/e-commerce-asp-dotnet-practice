# HTTP Contract: Sign Up, Sign In, Stay Signed In

> Written on 2026-09-27, after the feature merged (#44), from the code at that merge, the pull request
> and docs/architecture/storefront.md and docs/features/auth/security-best-practices.md.

**Feature**: [spec.md](../spec.md)

One endpoint is new; the other three were already there and are recorded because the client depends on
their exact behaviour. All on Identity (`:5056`), through the gateway at `http://localhost:5000/api/auth/...`.
Errors are RFC 7807 ProblemDetails.

---

## `POST /api/auth/logout` - anonymous - **new**

No body. Reads the `refreshToken` cookie if there is one.

| Situation | Status | Effect |
| :--- | :--- | :--- |
| A valid session cookie | `204` | the refresh token row is deleted; `Set-Cookie: refreshToken=; expires=Thu, 01 Jan 1970 ...; secure; samesite=lax; httponly` |
| No cookie, an empty one, an unknown or already-deleted token | `204` | nothing deleted; the cookie is cleared anyway |

Never 401, never 404: an expired access token or a missing session must not stop somebody signing out.
The access token already issued keeps working until it expires (15 minutes) - access tokens are
stateless (Bruno `auth/logout` docs).

## Endpoints the client relies on (unchanged)

### `POST /api/auth/register` - anonymous

Body `{ "email", "password", "firstName", "lastName" }`. `200` with
`{ "id", "email", "firstName", "lastName", "token", "refreshToken": "" }` and
`Set-Cookie: refreshToken=...; path=/; secure; samesite=lax; httponly` (7 days). The refresh token is
blanked in the body on purpose. `409` for an email already registered. **No validation at this merge**
(#43): nonsense is accepted with `200`.

### `POST /api/auth/login` - anonymous

Body `{ "email", "password" }`. `200` as register. `401` with one message for a wrong password and an
unknown email alike (#28).

### `POST /api/auth/refresh` - anonymous, cookie only

No body. `200` as register, with a **new** refresh token in the cookie - the one presented is removed.
`401` when there is no cookie, or the token is unknown, expired or revoked. After a logout it is `401`
(Bruno `auth/refresh after logout is 401`).

---

## How the client uses them

| Client action | Call | On failure |
| :--- | :--- | :--- |
| Page load | `refresh` | forget user and token; pages behind `RequireAuth` redirect to `/sign-in` |
| Any request answering 401 | one shared `refresh`, then the request once more | the 401 stands |
| Sign up | `register` | 409 → "An account with this email already exists. Sign in instead."; 400 → field errors and "Some details need fixing." |
| Sign in | `login` | 401 → the one message; else a generic retry message |
| Sign out | `logout`, then forget | forgets anyway |

All four are sent with `anonymous: true` - no bearer token and no refresh-and-retry, so a failed refresh
cannot trigger another refresh.
