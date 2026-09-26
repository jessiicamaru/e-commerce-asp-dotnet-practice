# HTTP Contract: Change your password and your name

> Written on 2026-09-27, after the feature merged (#147), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](../spec.md)

Three signed-in endpoints on Identity's `AuthController`, through the gateway on `:5000`. `/api/auth/me` goes
through the existing `/api/auth/{**catch-all}` route; `/api/auth/me/password` has its own route,
`auth-change-password-route`, under the `sign-in` rate-limit policy (specs/062). No message or gRPC service changed.

---

## `GET /api/auth/me` - signed in

| Status | Body |
| :--- | :--- |
| **200** | `{ "email": "lan@example.test", "firstName": "Lan", "lastName": "Pham", "phone": null, "emailConfirmed": true }` |
| 401 | no or invalid access token |

Always the caller's own account, from the token.

---

## `PUT /api/auth/me` - signed in

```json
{ "firstName": "Mai", "lastName": "Pham", "phone": "0912 345 678" }
```

| Status | When | Body |
| :--- | :--- | :--- |
| **200** | Saved | the same shape as `GET` |
| 400 | A name empty or over 100 characters; the phone over 20 | ProblemDetails with `errors` |
| 401 | no or invalid access token | - |

The email cannot be changed here. Records `ProfileUpdated` (User) with before and after.

---

## `PUT /api/auth/me/password` - signed in

```json
{ "currentPassword": "Passw0rd!23", "newPassword": "NewPassw0rd!45" }
```

The `refreshToken` cookie, when sent, names the session to keep.

| Status | When | Body |
| :--- | :--- | :--- |
| **204** | Changed; every other session ended, the cookie's kept | empty |
| 400 | The current password is wrong (counted toward the pause) | ProblemDetails, `errors.CurrentPassword`: `["Your current password is not correct."]` |
| 400 | Current password empty; new password breaking registration's rules | ProblemDetails with `errors` |
| 401 | no or invalid access token | - |
| 429 | The email's sign-in is paused (Identity), or the client's `sign-in` allowance is used up (gateway) | ProblemDetails with `retryAfter` and `Retry-After` |

Records `PasswordChanged` (Security), holding no password.

---

## Storefront calls

| Function (`client/src/services/auth/index.ts`) | Hook (`client/src/hooks/me/index.ts`) | Request |
| :--- | :--- | :--- |
| `Auth.me()` | `useMe` | `GET /auth/me` |
| `Auth.updateMe(input)` | `useUpdateMe` (then `refreshSession()`) | `PUT /auth/me` |
| `Auth.changePassword(current, next)` | `useChangePassword` | `PUT /auth/me/password` |

## Bruno

| Request | Asserts |
| :--- | :--- |
| `auth/my details.yml` | 200 with the run's own customer |
| `auth/change my details.yml` | 200 with the new name |
| `auth/changing my password with a wrong current one is 400.yml` | 400 |
| `auth/change my password.yml` | 204, on a throwaway account its pre-request registers |
| `security-checks/my details without a token is 401.yml` | 401 |
