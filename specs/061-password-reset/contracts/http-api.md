# HTTP Contract: Password reset

> Written on 2026-09-27, after the feature merged (#144), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](../spec.md)

Two anonymous endpoints on Identity's `AuthController`, reached through the gateway on `:5000` by the existing
`/api/auth/{**catch-all}` route (specs/062 later gave each its own route and rate-limit policy). No message and no
gRPC service changed. Errors are RFC 7807 ProblemDetails through `GlobalExceptionHandler`.

---

## `POST /api/auth/forgot-password` - anonymous

```json
{ "email": "lan@example.test" }
```

Header `Accept-Language` chooses the email's language (first entry, primary tag; none means Vietnamese).

| Status | When | Body |
| :--- | :--- | :--- |
| **202** | Always, for any well-formed request: an address with an account, one without, a banned one | empty |
| 400 | `email` missing or over 255 characters | ProblemDetails with `errors.Email` |

For a real account it stores a hashed token, deletes that person's earlier unused links, records
`PasswordResetRequested` and queues the `PasswordReset` email. Nothing in the answer differs (#28).

---

## `POST /api/auth/reset-password` - anonymous

```json
{ "token": "<the token from the link>", "password": "a new password" }
```

| Status | When | Body |
| :--- | :--- | :--- |
| **204** | The link was unused and unexpired; the password is set and every session ended | empty |
| 400 | The token is used, expired, replaced, made up, empty or over 200 characters | ProblemDetails, `errors.Token`: `["This link is invalid or has expired. Ask for a new one."]` |
| 400 | The password breaks registration's rules (minimum length, at most the maximum bytes) | ProblemDetails, `errors.Password` |

A password-rule 400 does not use the link: validation runs before the claim.

---

## The link in the email

`{Email:StorefrontUrl}/reset-password?token={token}` - `http://localhost:8088` in the containers,
`STOREFRONT_URL` in `.env` for `start-dev`. The storefront's `/reset-password` reads `?token` and posts it.

---

## Storefront calls

| Function (`client/src/services/auth/index.ts`) | Request |
| :--- | :--- |
| `forgotPassword(email)` | `POST /auth/forgot-password`, anonymous |
| `resetPassword(token, password)` | `POST /auth/reset-password`, anonymous |

## Bruno

| Request | Asserts |
| :--- | :--- |
| `bruno/auth/forgot password is 202 for anybody.yml` | 202 with an empty body for a made-up address |
| `bruno/security-checks/reset with a made-up token is 400.yml` | 400 for a token that was never issued |
