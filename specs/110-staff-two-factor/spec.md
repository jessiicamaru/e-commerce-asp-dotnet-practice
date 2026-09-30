# Feature Specification: Staff sign in with a second factor

**Feature Branch**: `110-staff-two-factor` | **Created**: 2026-09-30 | **Issue**: #218

**Status**: Draft

**Input**: Issue #218 - "staff accounts sign in with a password alone".

## Why

An administrator can ban people, close shops, cancel orders, record payouts and, since specs/106, read every seller's
full bank account number. A moderator can lock accounts and take products down. All of it sits behind one password.
A password reused elsewhere and leaked, or guessed despite specs/062's limits, hands over the shop.

## User Scenarios & Testing *(mandatory)*

### US1 - A staff member enrols an authenticator app (Priority: P1)

An administrator or moderator without a second factor signs in with their password and is taken straight to "Set up
two-factor sign-in". Until they finish, they hold only their non-staff roles: they can shop, but every staff page and
endpoint in every service refuses them.

To finish, they:
1. scan a QR code with Google Authenticator, Microsoft Authenticator, Authy or similar;
2. type the code the app shows;
3. receive ten recovery codes, shown once.

Their session then holds their staff roles.

**Why this priority**: Without enrolment nothing else in this feature can happen.

**Acceptance Scenarios**:

1. **Given** a moderator without 2FA, **When** they sign in, **Then** the answer says setup is required and their token
   carries no `Moderator` role. A staff endpoint in another service (Catalog's review queue) answers 403.
2. **Given** setup started, **When** they confirm with a wrong code, **Then** 400 and 2FA stays off.
3. **Given** the right code, **Then** 2FA is on, ten recovery codes are returned once, and after the session renews
   the token carries `Moderator`.
4. **Given** setup already confirmed, **When** setup is asked again, **Then** 409.

---

### US2 - Signing in takes a code (Priority: P1)

Anybody with 2FA on signs in in two steps:
1. The right password answers with a short-lived challenge, and nothing that grants access.
2. The code from the app, or one recovery code, exchanges the challenge for the session.

**Acceptance Scenarios**:

1. **Given** 2FA on, **When** the right password is sent, **Then** no access token and no refresh cookie are issued;
   only a challenge.
2. **Given** the challenge and the current code, **Then** a full session is issued, with staff roles.
3. **Given** a code already used (same 30-second window), **Then** 400: a code works once.
4. **Given** a recovery code, **Then** it works once, and the same code again is 400.
5. **Given** wrong codes, **Then** they count toward the sign-in pause (specs/062), and a challenge dies after five.
6. **Given** a challenge older than five minutes, **Then** 400; sign in again.
7. **Given** 2FA on and the wrong password, **Then** the ordinary 401. The challenge step is never reached, and nothing
   says the account uses 2FA.

---

### US3 - A lost phone (Priority: P2)

- A person with 2FA on can make a fresh set of recovery codes, which requires a current code.
- An administrator can reset another staff member's 2FA. This is audited, and the owner is emailed at once.
- Nobody can reset their own 2FA this way.

**Acceptance Scenarios**:

1. **Given** an administrator, **When** they reset a moderator's 2FA, **Then** the moderator's sessions end, the
   moderator is emailed, and their next sign-in requires setup again.
2. **Given** a moderator, **When** they try to reset anyone's 2FA, **Then** 403.
3. **Given** an administrator, **When** they try to reset their own, **Then** 403.

---

### US4 - Anybody may turn it on (Priority: P3)

A customer or seller may enrol too, from their account page, and may turn it off again with their password and a
code. Staff cannot turn it off.

### Edge Cases

- **Existing staff sessions.** A refresh token issued before this feature was not verified with a code, so the
  session renews without staff roles. That is the same as a staff member who has not enrolled.
- **Enrolling ends every other session.** None of them were verified with a code. The session that enrolled continues.
- **Clock drift**: the window before and the window after are accepted (±30 s).
- **Replay**: a code whose window is at or before the last one used is refused, even if it is otherwise valid.
- **The secret at rest** is encrypted with a key from configuration (`TWO_FACTOR_KEY`). Identity refuses to start
  without a usable key, as it does for the JWT secret.
- **Development and CI**: `ADMIN_TOTP_SECRET`, when set, enrols the seeded administrator with a known secret. The
  scripts, Bruno and Playwright compute its codes. It stays unset in production.
- **A role granted later** (Moderator) needs enrolment before it takes effect. This is the same rule as everywhere.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: TOTP per RFC 6238: SHA-1, 6 digits, 30 s, a ±1-step window, and the last used step stored. It is
  verified by the RFC's Appendix B vectors.
- **FR-002**: `users` gains:
  - `TwoFactorSecret`, encrypted;
  - `TwoFactorEnabledAt`;
  - `TwoFactorLastStep`.

  New tables `two_factor_recovery_codes` (hashes) and `two_factor_challenges` (hash, expiry, failed attempts, used).
  `refresh_tokens` gains `TwoFactorVerified`.
- **FR-003**: Staff roles (`Admin`, `Moderator`) are written into an access token only when its session was verified
  with a second factor. `AuthResponse.Roles` lists the same roles as the token.
- **FR-004**: `POST /api/auth/login` answers a user with 2FA on with `twoFactor: "Required"` and a `challenge`, and
  issues no token or cookie. It answers staff without 2FA with `twoFactor: "SetupRequired"` and a session without staff
  roles.
- **FR-005**: `POST /api/auth/login/two-factor` (anonymous, sign-in rate limit) takes `{challenge, code}` or
  `{challenge, recoveryCode}` and returns the full session.
- **FR-006**: The signed-in caller's own 2FA:
  - `GET /api/auth/me/two-factor`;
  - `POST .../setup`, which returns the secret and `otpauth://` URI once;
  - `POST .../confirm`, which returns ten recovery codes;
  - `POST .../recovery-codes`, which needs a code;
  - `DELETE /api/auth/me/two-factor`, which needs the password and a code, and is refused to staff.
- **FR-007**: `DELETE /api/users/{id}/two-factor` (Admin, never oneself) resets it. It revokes sessions, is audited, and
  emails `TwoFactorReset`.
- **FR-008**: Every enable, disable, reset, recovery-code use and new set is audited under Security.
- **FR-009**: The storefront:
  - a second step on `/sign-in`;
  - `/account/two-factor`, with the QR code, confirmation, recovery codes, a new set and turning off;
  - staff sent there while setup is required;
  - "Reset two-factor" on an administrator's person page.

## Success Criteria *(mandatory)*

- **SC-001**: A staff token without a verified second factor is refused by a staff endpoint in a service other than
  Identity. This is checked through the gateway in Bruno and in `verify-auth.sh`.
- **SC-002**: The TOTP implementation reproduces RFC 6238's SHA-1 vectors.
- **SC-003**: CI's scripts, Bruno and Playwright sign in the administrator with a code, and the whole suite stays green.

## Assumptions

- TOTP only. There is no SMS or email code (they are weaker and cost money), and no WebAuthn yet (the stronger
  successor, and a larger change).
- One authenticator per account. Re-enrolling replaces it, after a reset.
