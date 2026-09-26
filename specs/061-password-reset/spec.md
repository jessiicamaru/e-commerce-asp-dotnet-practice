# Feature Specification: Password reset

> Completed on 2026-09-27, after the feature merged (#144), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md) with
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature Branch**: `061-password-reset` | **Created**: 2026-09-25 | **Issue**: #103

**Status**: Merged (#144, 2026-09-25). Later work touched this flow: specs/062 limits how often a link can be asked
for, specs/065 ends signed-in access tokens on a reset, specs/077 makes the email's words editable.

**Input**: Issue #103: a person who forgets their password has no way back into their account.

## Why

Somebody who forgets their password has lost their account. There is no "forgot password" link, no
endpoint and no email. Email exists since specs/060, so a reset link can now be sent.

## User Scenarios & Testing *(mandatory)*

### US1 - Ask for a link (Priority: P1)

A person on the sign-in page follows "Forgot password?", types their email and is told: "If an account
uses this address, we have sent a link to reset its password." They are told exactly the same thing
whether or not the address has an account.

**Why this priority**: Without it the rest cannot start. And the answer is where the feature could leak which
addresses have accounts (#28), so it has to be right first.

**Independent Test**: `POST /api/auth/forgot-password` with a real address and with a made-up one: both 202 with
an empty body; only the real one leaves a `PasswordReset` email in Mailpit.

**Acceptance** (as first written)
1. `POST /api/auth/forgot-password {email}` answers **202** with no body for a known address, an
   unknown one, and a banned one alike (#28: the answer must not reveal which emails exist).
2. For a real account, one `PasswordReset` email is queued, in the language the request came in, with
   a link to `/reset-password?token=…`. The link works for **30 minutes** and **once**.
3. Asking again replaces the earlier link: only the newest one works.

**Acceptance Scenarios**:

1. **Given** an address with an account, **When** a reset is asked for with `Accept-Language: en`, **Then** the
   answer is 202 with no body, one `password_reset_tokens` row holds a 64-character hash and an expiry 30 minutes
   ahead, and one English "Reset your password" email is queued with a link to `/reset-password?token=...`.
2. **Given** an address with no account, **When** a reset is asked for, **Then** the answer is the same 202 and
   nothing is stored or sent.
3. **Given** a person who asked twice, **When** they open the first link, **Then** it is refused; the second works.

---

### US2 - Choose a new password (Priority: P1)

The link opens a page with a new password field. On success the person is sent to sign in.

**Why this priority**: It is the half that gives the account back.

**Independent Test**: Open the link from Mailpit, choose a new password: the new one signs in, the old one does not,
and a session from before the reset can no longer refresh.

**Acceptance** (as first written)
1. `POST /api/auth/reset-password {token, password}` applies the same password rules as registration.
2. On success the new password signs in, and the old one does not.
3. **Every existing session ends**: old refresh tokens are refused, as after a lock (specs/043).
4. A used, expired, replaced or unknown token gets the same **400** "This link is invalid or has
   expired. Ask for a new one."
5. Two submissions of one link at once: one succeeds, the other gets that 400.

**Acceptance Scenarios**:

1. **Given** a valid link, **When** a password shorter than registration allows is sent, **Then** 400 with the
   password rule beside the field, and the link is still usable.
2. **Given** a valid link, **When** a good password is sent, **Then** 204; the new password signs in (200), the old
   one gets 401, and a refresh token from before gets 401.
3. **Given** a link already used, **When** it is sent again, **Then** 400 on `Token` with "This link is invalid or
   has expired. Ask for a new one."
4. **Given** one link, **When** two submissions arrive at once, **Then** exactly one returns 204.
5. **Given** a link with no token, **When** the page opens, **Then** it says the link is broken and offers no form.

---

### US3 - On the record, with nothing secret in it (Priority: P2)

`PasswordResetRequested` (only for a real account) and `PasswordReset` are recorded under Security. The
entries hold neither the token nor any password. After the email is sent, its stored data no longer
holds the token either.

**Why this priority**: The flow works without it, but a reset is exactly the event an administrator needs to see
when an account is disputed, and a credential left in a table is a second way in.

**Independent Test**: After a reset, `GET /api/audit?category=Security` shows both entries with no token in them,
and the sent email's row has `DataJson` = `{}`.

**Acceptance Scenarios**:

1. **Given** a reset asked for and completed, **When** the audit log is read, **Then** it holds
   `PasswordResetRequested` and `PasswordReset` with the person as actor, and neither contains the token or a
   password.
2. **Given** the reset email was sent, **When** its `outgoing_emails` row is read, **Then** `DataJson` is `{}`.

---

### Edge Cases

- **A banned or locked account** gets the same 202 and a link; resetting the password does not lift the stop -
  sign-in still refuses it.
- **An email that is still `Pending` or `Failed`** keeps the token in its row, but only for as long as the link
  could work anyway (30 minutes).
- **A link opened after another was asked for.** Replaced links are deleted, so the old one is "invalid".
- **The token in the URL** is escaped with `Uri.EscapeDataString`; it is base64url, so nothing needs escaping in
  practice.
- **No `Accept-Language`.** The email is Vietnamese.
- **A network failure on `/forgot-password`** shows the generic error rather than claiming a link was sent.

## Requirements *(mandatory)*

### Functional Requirements

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
- **FR-005**: Asking for a link MUST answer 202 with no body for every address.
- **FR-006**: A link MUST work once and for 30 minutes; asking again MUST make earlier unused links stop working.
- **FR-007**: A reset MUST apply registration's password rules and MUST end every session of the account.
- **FR-008**: Every refused token MUST get the same 400 on `Token`.
- **FR-009**: Both steps MUST be recorded under Security with no secret in them.

### Key Entities

- **Reset token** (`password_reset_tokens`): whose, the hash, when it expires, when it was used.
- **Reset email** (`outgoing_emails`, template `PasswordReset`): the one row holding the token until sent.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A known and an unknown address get byte-identical answers (202, empty body).
- **SC-002**: A person who asks can choose a new password from the email within 30 minutes, and afterwards no
  session from before the reset refreshes.
- **SC-003**: Of two simultaneous submissions of one link, exactly one resets.
- **SC-004**: No token or password appears in any audit entry, and none remains in a sent email's row.

## Assumptions

- Email delivery (specs/060) works and the storefront is at `Email:StorefrontUrl` for the link.
- The storefront sends `Accept-Language` on every request.
- 30 minutes is long enough to open an email and short enough to limit a stolen inbox's window (the length was
  set by the design; no measurement is recorded).

## Out of scope

- Limiting how often a reset can be asked for: #105 (sign-in rate limits) covers the auth endpoints
  together.
- Changing a password while signed in: #104.
