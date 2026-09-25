# Feature Specification: Change your password and your name

**Feature Branch**: `064-account-settings` | **Created**: 2026-09-25 | **Issue**: #104

## Why

Once registered, a person cannot change their password, first name, last name or phone. The account page
shows them and offers nothing.

## User Scenarios

### US1 - Change my password (P1)

**Acceptance**
1. `PUT /api/auth/me/password {currentPassword, newPassword}` is signed in; the account comes from the
   token (constitution IV). It answers **204**.
2. The current password is checked. A wrong one is **400** on `CurrentPassword`, and nothing changes.
3. A wrong current password counts against the email's sign-in pause (specs/062). Otherwise a stolen
   access token could guess the password faster than the sign-in page allows.
4. The new password follows registration's rules.
5. Success revokes **every other** session and keeps this one: the browser that made the change stays
   signed in, and another browser is signed out at its next refresh.
6. It is a **Security** audit entry, `PasswordChanged`, holding no password.

### US2 - Change my name and phone (P1)

**Acceptance**
1. `GET /api/auth/me` returns the caller's own email, first name, last name, phone and `emailConfirmed`.
2. `PUT /api/auth/me {firstName, lastName, phone}` changes them and returns the same shape. It is refused
   400 when a name is empty or too long, or the phone is too long.
3. It is a **User** audit entry, `ProfileUpdated`, with the fields before and after.
4. The storefront renews the session afterwards, so the header and the `given_name` claim (used to sign
   reviews) carry the new name.

### US3 - The account page (P1)

The storefront's account page gets two forms, "Your details" and "Change password". The server's
refusals are shown in its words.

## Out of scope

- Changing the email address. It needs its own confirmation of the new address.
- An email telling the person their password was changed.
