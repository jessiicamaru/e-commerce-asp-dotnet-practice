# HTTP Contract: Limits on the sign-in and email endpoints

> Written on 2026-09-27, after the feature merged (#145), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](../spec.md)

No endpoint was added. Six existing Identity endpoints gained their own gateway routes with a rate-limit policy,
and two of them gained a new 429 from Identity itself. No message and no gRPC service changed.

---

## Gateway routes and policies

Routes in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`, all to `identity-cluster`, more specific
than `/api/auth/{**catch-all}` so they win it:

| Route | Path | `RateLimiterPolicy` | Default, per client IP |
| :--- | :--- | :--- | :--- |
| `auth-login-route` | `/api/auth/login` | `sign-in` | 30 per 60 s, shared by the four `sign-in` paths |
| `auth-register-route` | `/api/auth/register` | `sign-in` | |
| `auth-register-seller-route` | `/api/auth/register-seller` | `sign-in` | |
| `auth-reset-password-route` | `/api/auth/reset-password` | `sign-in` | |
| `auth-forgot-password-route` | `/api/auth/forgot-password` | `email` | 5 per 60 s |
| `auth-refresh-route` | `/api/auth/refresh` | `session` | 60 per 60 s |

Every other route is unlimited. The client IP is the connection's, or - only when the peer is in
`GATEWAY_TRUSTED_PROXIES` - the last hop of `X-Forwarded-For`.

---

## The 429 - from the gateway

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 42
Content-Type: application/problem+json

{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Too many attempts. Try again in 42 seconds.",
  "instance": "/api/auth/forgot-password",
  "retryAfter": 42
}
```

`Retry-After` and `retryAfter` are the same whole number of seconds, from the lease's `RetryAfter` metadata,
rounded up and at least 1 (60 when the lease does not say). The request never reaches Identity.

---

## The 429 - from Identity: `POST /api/auth/login` while an email is paused

After 5 wrong passwords for one email within 15 minutes (the fifth still gets the ordinary 401):

```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Too many wrong passwords for this email. Try again later.",
  "retryAfter": 300,
  "traceId": "..."
}
```

With a `Retry-After` header of the same seconds. `TooManyRequestsException` (`Ecommerce.Shared.Exceptions`) is
mapped by `GlobalExceptionHandler`, and its message is shown in every environment, like `ForbiddenException`. It
is the same for an address with no account (#28).

| Status for `POST /api/auth/login` | When |
| :--- | :--- |
| 200 | The right password, no pause (the count is cleared) |
| 401 | A wrong password or unknown email - counted |
| 403 | The right password on a locked or banned account (specs/049), unchanged |
| **429** | The email is paused - whatever the password |

---

## `POST /api/auth/forgot-password` - one email a minute per address

The answer is **202** as before, always. A request for an address that was sent a link in the last minute queues
nothing. Many requests at once queue one.

## `POST /api/auth/reset-password`

Unchanged in shape; a successful reset also clears the email's wrong-password count.

---

## Storefront reading

`ApiError.retryAfterSeconds` (`client/src/config/axios/api-error.ts`) reads `retryAfter` from the body, then the
`Retry-After` header; `tooManyAttempts(t, error)` (`client/src/utils/shared/too-many.ts`) words it as whole
minutes, rounded up, never zero, or "try again later" with no number.

## Bruno

`bruno/security-checks/asking for reset links too fast is 429.yml` (`seq: 52`, the last of `security-checks`): its
pre-request script sends five `forgot-password` requests, catching a 429 because Bruno's sandboxed axios throws on
one; the request is then refused with 429 and `Retry-After` between 1 and 60 equal to `retryAfter`.
