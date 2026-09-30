# HTTP contract: two-factor sign-in

## `AuthResponse` (every path that issues a session) - two new fields

```json
{
  "id": "…", "email": "…", "firstName": "…", "lastName": "…",
  "token": "…", "roles": ["Customer"], "emailConfirmed": true,
  "twoFactor": null | "Required" | "SetupRequired",
  "challenge": null | "…"
}
```

- `"Required"`:
  - `token` is empty and no refresh cookie is set;
  - `roles` is empty;
  - `challenge` is set.
- `"SetupRequired"`: a session without staff roles. The storefront sends the person to `/account/two-factor`.
- `roles` is always what the access token carries.

## `POST /api/auth/login` (anonymous, `sign-in` rate limit)

This is unchanged except for the two fields above. The wrong password is still the one 401.

## `POST /api/auth/login/two-factor` (anonymous, `sign-in` rate limit)

The request carries exactly one of `code` or `recoveryCode`:

```json
{ "challenge": "…", "code": "123456" }
{ "challenge": "…", "recoveryCode": "ABCDE-FGHIJ" }
```

| Answer | When |
| :-- | :-- |
| 200 `AuthResponse` + refresh cookie | The code is right. `roles` includes staff roles. |
| 400 on `code` - "The code is not right, or was already used." | Wrong, replayed, or the recovery code is already spent. |
| 400 on `challenge` - "Sign in again." | Expired, used, unknown, or 5 wrong codes. |
| 429 | The email pause of specs/062 (wrong codes count). |

## The caller's own second factor (signed in)

| Method | Path | Body | Answers |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/auth/me/two-factor` | - | `{enabled, enabledAt, recoveryCodesLeft, required}` |
| `POST` | `/api/auth/me/two-factor/setup` | - | `{secret, uri}` (base32 and `otpauth://…`) - 409 when already on |
| `POST` | `/api/auth/me/two-factor/confirm` | `{code}` | `{recoveryCodes: [10]}` - 400 wrong code; 409 not set up or already on. This session becomes verified, and the others end. |
| `POST` | `/api/auth/me/two-factor/recovery-codes` | `{code}` | `{recoveryCodes: [10]}`; the old set stops working |
| `DELETE` | `/api/auth/me/two-factor` | `{password, code}` | 204; 403 for staff; 400 wrong password or code |

After `confirm`, the storefront renews its session (`POST /api/auth/refresh`), which now carries the staff roles.

## Staff

| Method | Path | Who | Answers |
| :-- | :-- | :-- | :-- |
| `DELETE` | `/api/users/{id}/two-factor` | Admin | 204; 403 oneself or not an administrator; 404 unknown; 409 not on |

## Email

`TwoFactorReset` is sent to the owner in their language. Placeholders: `{name}`, `{link}` (the sign-in page).

## Gateway

A new route, `/api/auth/login/two-factor`, uses the `sign-in` rate limiter. It is declared before
`/api/auth/{**catch-all}`.
