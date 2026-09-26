# HTTP Contract: Email confirmation

> Written on 2026-09-27, after the feature merged (#146), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](../spec.md)

Two new endpoints on Identity's `AuthController`, one field on every authentication response, and two new
refusals on shop applications. All through the gateway on `:5000`. No message or gRPC service changed.

---

## `POST /api/auth/confirm-email` - anonymous

Gateway route `auth-confirm-email-route`, policy `sign-in` (a guessable-token endpoint, specs/062).

```json
{ "token": "<the token from the link>" }
```

| Status | When | Body |
| :--- | :--- | :--- |
| **204** | The link was unused and unexpired; the address is now confirmed (or already was - the link is spent, nothing changes) | empty |
| 400 | Used, expired, replaced, made up, empty or over 200 characters | ProblemDetails, `errors.Token`: `["This link is invalid or has expired."]` |
| 429 | The client's `sign-in` allowance is used up (gateway) | ProblemDetails with `retryAfter` |

---

## `POST /api/auth/resend-confirmation` - signed in

Gateway route `auth-resend-confirmation-route`, policy `email`. No body; the account comes from the access token.
`Accept-Language` chooses the email's language.

| Status | When | Body |
| :--- | :--- | :--- |
| **202** | A new link was sent and the earlier one deleted - or one was sent less than a minute ago and nothing was sent | empty |
| 401 | No or invalid access token | - |
| 409 | The address is already confirmed | ProblemDetails, "This email address is already confirmed." |
| 429 | The client's `email` allowance is used up (gateway) | ProblemDetails with `retryAfter` |

---

## `emailConfirmed` on every authentication response

`POST /api/auth/register`, `/register-seller`, `/login` and `/refresh` return `AuthResponse`, which gains:

```json
{ "id": "...", "email": "...", "firstName": "...", "lastName": "...", "token": "...",
  "roles": ["Customer"], "emailConfirmed": false }
```

It is for drawing the banner, like `roles` - never for deciding. Additive: the storefront reads a missing value as
`true`. `register` and `register-seller` now also read `Accept-Language` for the confirmation email.

---

## Shop applications (specs/044), new refusals

| Endpoint | New answer |
| :--- | :--- |
| `POST /api/shop-applications` (Customer) | **403** ProblemDetails, detail "Confirm your email address before applying to sell.", fact `code: "EmailNotConfirmed"` |
| `POST /api/shop-applications/{id}/approve` (Staff) | **409** "The applicant has not confirmed their email address yet." while the applicant is unconfirmed and the application pending |
| `GET /api/shop-applications` (Staff) | each item gains `applicantEmailConfirmed` (bool; null in the applicant's own view) |

---

## The link in the email

`{Email:StorefrontUrl}/confirm-email?token={token}`. The storefront's `/confirm-email` posts it once and, when a
session is signed in, renews it so the banner goes.

## Bruno

| Request | Asserts |
| :--- | :--- |
| `seller/approving before the address is confirmed is 409.yml` | 409 |
| `seller/the seller confirms their email with the link.yml` | reads the link from Mailpit (`mailpitUrl`), then 204 |
| `security-checks/confirming with a made-up token is 400.yml` | 400 |
| `security-checks/sending a confirmation link without a token is 401.yml` | 401 |
