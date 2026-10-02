# Contracts: A one-time handoff from the storefront to the back office

## HTTP (Identity, through the gateway)

| Method | Path | Who | Body | Answer |
| :-- | :-- | :-- | :-- | :-- |
| `POST` | `/api/auth/handoff` | signed in | - | `200 { "code": "..." }`; 403 `{ code: NotStaff | TwoFactorSetupRequired }` |
| `POST` | `/api/auth/handoff/redeem` | anonymous, rate-limited (`sign-in`) | `{ "code": "..." }` | `200` an `AuthResponse` with `twoFactor: "Required"` and `challenge`; 400 on `Code` for used, expired and made-up codes |

The challenge then goes to the existing `POST /api/auth/login/two-factor`.

## Back office

`/auth/callback#code=<code>`.
