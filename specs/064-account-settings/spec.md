# Feature Specification: Change your password and your name

> Completed on 2026-09-27, after the feature merged (#147), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature Branch**: `064-account-settings` | **Created**: 2026-09-25 | **Issue**: #104

**Status**: Merged (#147, 2026-09-25). Later: specs/065 also ends the other sessions' **access** tokens on a
password change.

**Input**: Issue #104: a person cannot change their password or their details after registering.

## Why

Once registered, a person cannot change their password, first name, last name or phone. The account page
shows them and offers nothing.

## User Scenarios & Testing *(mandatory)*

### US1 - Change my password (Priority: P1)

**Why this priority**: A password a person believes is known to somebody else must be changeable without the
detour of "forgot password", and changing it must shut out whoever else holds a session.

**Independent Test**: Signed in on two browsers, change the password on the first: 204; the first still refreshes,
the second gets 401 at its next refresh; the old password no longer signs in.

**Acceptance** (as first written)
1. `PUT /api/auth/me/password {currentPassword, newPassword}` is signed in; the account comes from the
   token (constitution IV). It answers **204**.
2. The current password is checked. A wrong one is **400** on `CurrentPassword`, and nothing changes.
3. A wrong current password counts against the email's sign-in pause (specs/062). Otherwise a stolen
   access token could guess the password faster than the sign-in page allows.
4. The new password follows registration's rules.
5. Success revokes **every other** session and keeps this one: the browser that made the change stays
   signed in, and another browser is signed out at its next refresh.
6. It is a **Security** audit entry, `PasswordChanged`, holding no password.

**Acceptance Scenarios**:

1. **Given** a signed-in person on browsers A and B, **When** A changes the password with the right current one,
   **Then** 204; A's refresh is 200, B's is 401; the old password gets 401 and the new one signs in.
2. **Given** a wrong current password, **When** it is sent, **Then** 400 on `CurrentPassword` ("Your current
   password is not correct."), the password is unchanged, and one failure is counted for the email.
3. **Given** five wrong current passwords, **When** the next change is attempted - even with the right one -
   **Then** 429 with `Retry-After`.
4. **Given** a request with no refresh cookie, **When** the password changes, **Then** every session ends.
5. **Given** a successful change, **When** the audit log is read, **Then** one `PasswordChanged` under Security,
   with no password in it.

---

### US2 - Change my name and phone (Priority: P1)

**Why this priority**: A name typed wrongly at registration signs every review and appears on every order; the
person has no way to correct it.

**Independent Test**: `GET /api/auth/me`, then `PUT /api/auth/me` with a new first name: the answer and the next
`GET` show it, and the audit log has `ProfileUpdated` with before and after.

**Acceptance** (as first written)
1. `GET /api/auth/me` returns the caller's own email, first name, last name, phone and `emailConfirmed`.
2. `PUT /api/auth/me {firstName, lastName, phone}` changes them and returns the same shape. It is refused
   400 when a name is empty or too long, or the phone is too long.
3. It is a **User** audit entry, `ProfileUpdated`, with the fields before and after.
4. The storefront renews the session afterwards, so the header and the `given_name` claim (used to sign
   reviews) carry the new name.

**Acceptance Scenarios**:

1. **Given** a signed-in person, **When** they `GET /api/auth/me`, **Then** their own email, names, phone and
   `emailConfirmed` - and nobody else's, whatever the request says.
2. **Given** an empty first name, **When** `PUT /api/auth/me` is sent, **Then** 400 and nothing changes.
3. **Given** a blank phone, **When** it is sent, **Then** the phone is stored as none.
4. **Given** no access token, **When** `GET /api/auth/me` is called, **Then** 401.

---

### US3 - The account page (Priority: P1)

The storefront's account page gets two forms, "Your details" and "Change password". The server's
refusals are shown in its words.

**Why this priority**: The endpoints are only useful to a person through the page.

**Independent Test**: `cd client && npm test -- src/pages/account` - the page shows the details, sends changes,
refuses mismatched new passwords, and shows a wrong current password beside its field and a 429 as a wait.

**Acceptance Scenarios**:

1. **Given** the account page, **When** the details are changed and saved, **Then** the change is sent and the
   session renewed.
2. **Given** two different new passwords, **When** the form is submitted, **Then** nothing is sent.
3. **Given** a 400 on `CurrentPassword`, **When** it returns, **Then** the message is shown beside that field.
4. **Given** a 429, **When** it returns, **Then** the page says how long to wait (specs/062).

---

### Edge Cases

- **The kept session** is named by the HttpOnly cookie, never by the body; a body field of that name is
  overwritten by the controller.
- **The email is not editable** here: a new address would need its own confirmation (specs/063).
- **An access token already issued to another browser** keeps working until it expires; only its refresh is
  refused (closed later by specs/065).
- **A successful change clears the email's wrong-password count.**
- **A wrong current password records no audit entry**; it is counted toward the pause instead.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The three endpoints MUST read the account from the access token only.
- **FR-002**: A password change MUST check the current password, count a wrong one toward the specs/062 pause, and
  answer 429 while the email is paused.
- **FR-003**: A new password MUST follow registration's rules.
- **FR-004**: A password change MUST end every other session and keep the one named by the request's refresh
  cookie; with no cookie, every session ends.
- **FR-005**: The new password, its audit entry and the revocation MUST commit in one transaction.
- **FR-006**: A details change MUST validate names (required, at most 100) and phone (at most 20), and MUST record
  `ProfileUpdated` with before and after.
- **FR-007**: The storefront MUST offer both forms on the account page and renew the session after a details change.

### Key Entities

- **Account profile**: email (read only), first name, last name, phone, whether the email is confirmed.
- **Session** (`refresh_tokens`): the one that made the change is kept; the others are revoked.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a password change, 100% of the person's other sessions fail their next refresh, and the one
  that made the change keeps working.
- **SC-002**: Guessing the current password through this endpoint is no faster than through sign-in (the same 5
  per 15 minutes per email).
- **SC-003**: A name change is visible in the header and on new reviews after the storefront renews the session.

## Assumptions

- The refresh token lives in the HttpOnly `refreshToken` cookie the server sets (no `Path`, so it is sent with
  every request to the API, this one included).
- The sign-in pause of specs/062 is in place.

## Out of scope

- Changing the email address. It needs its own confirmation of the new address.
- An email telling the person their password was changed.
