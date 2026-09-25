# Feature Specification: Password reset

**Feature Branch**: `061-password-reset` | **Created**: 2026-09-25 | **Issue**: #103

## Why

Somebody who forgets their password has lost their account. There is no "forgot password" link, no
endpoint and no email. Email exists since specs/060, so a reset link can now be sent.

## User Scenarios

### US1 - Ask for a link (P1)

A person on the sign-in page follows "Forgot password?", types their email and is told: "If an account
uses this address, we have sent a link to reset its password." They are told exactly the same thing
whether or not the address has an account.

**Acceptance**
1. `POST /api/auth/forgot-password {email}` answers **202** with no body for a known address, an
   unknown one, and a banned one alike (#28: the answer must not reveal which emails exist).
2. For a real account, one `PasswordReset` email is queued, in the language the request came in, with
   a link to `/reset-password?token=…`. The link works for **30 minutes** and **once**.
3. Asking again replaces the earlier link: only the newest one works.

### US2 - Choose a new password (P1)

The link opens a page with a new password field. On success the person is sent to sign in.

**Acceptance**
1. `POST /api/auth/reset-password {token, password}` applies the same password rules as registration.
2. On success the new password signs in, and the old one does not.
3. **Every existing session ends**: old refresh tokens are refused, as after a lock (specs/043).
4. A used, expired, replaced or unknown token gets the same **400** "This link is invalid or has
   expired. Ask for a new one."
5. Two submissions of one link at once: one succeeds, the other gets that 400.

### US3 - On the record, with nothing secret in it (P2)

`PasswordResetRequested` (only for a real account) and `PasswordReset` are recorded under Security. The
entries hold neither the token nor any password. After the email is sent, its stored data no longer
holds the token either.

## Requirements

- **FR-001**: The token is 32 random bytes, sent base64url in the link and stored **only as its
  SHA-256 hash**. Anyone reading the database cannot use a link.
- **FR-002**: The reset claims the token in **one guarded statement**
  (`UPDATE ... WHERE "UsedAt" IS NULL AND "ExpiresAt" > now RETURNING "UserId"`), then sets the
  password and revokes the sessions, all in one transaction.
- **FR-003**: Identity is both the requester and the sender. The email is queued straight into its own
  `outgoing_emails` in the token's transaction, so the token never crosses the broker. The dispatcher
  scrubs the token from the row once the email is sent.
- **FR-004**: The language comes from the request's `Accept-Language`, which the storefront sends on
  every request, falling back to Vietnamese.

## Out of scope

- Limiting how often a reset can be asked for: #105 (sign-in rate limits) covers the auth endpoints
  together.
- Changing a password while signed in: #104.
