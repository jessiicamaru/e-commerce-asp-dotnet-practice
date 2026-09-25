# Implementation Plan: Change your password and your name

**Branch**: `064-account-settings` | **Spec**: [spec.md](spec.md) | **Issue**: #104

## Design

- **Commands** in `Application/Auth/Commands/Account/Account.cs`:
  - `GetMeQuery` → `AccountProfile(Email, FirstName, LastName, Phone, EmailConfirmed)`.
  - `UpdateMeCommand(FirstName, LastName, Phone)`.
  - `ChangePasswordCommand(CurrentPassword, NewPassword)`, with an init-only `KeepRefreshToken` that the
    controller fills from the HttpOnly cookie, never from the body.

  All three read the caller from `ICurrentUser`.
- **The password change**, in order:
  1. If the email's sign-in is paused, answer 429.
  2. Check the current password. When it is wrong, count the failure and answer 400.
  3. Otherwise set the hash, record the audit entry, save.
  4. Revoke the refresh tokens with `IUserRepository.RevokeOtherRefreshTokensAsync(userId, keep, now)`:
     every active one except the cookie's. With no cookie, every one.
- **Routes:** `AuthController` gets `GET me`, `PUT me` and `PUT me/password`, all `[Authorize]`. The
  gateway gives `/api/auth/me/password` the `sign-in` limit.
- **Storefront:**
  - `Auth.me()`, `Auth.updateMe()`, `Auth.changePassword()`;
  - `hooks/account` (`useMe`, `useUpdateMe`, `useChangePassword`);
  - the account page with the two forms. A profile change calls `refreshSession()`.
- **Bruno:** in `auth/`, "my profile", "update my name", "changing my password with a wrong current one
  is 400" and "change my password" (on a throwaway account the pre-request registers). In
  `security-checks/`, "my profile without a token is 401".

## Decisions

1. **The current session is kept; the others end.** The person who just proved the password should not be
   signed out. Whoever else holds a session, possibly the reason for the change, loses it. It is
   identified by the refresh cookie, which the server set and the script cannot read.
2. **A wrong current password counts against the sign-in pause.** Otherwise this endpoint is a way round
   specs/062 for anybody holding a stolen access token.
3. **No email change.** An unconfirmed new address would undo specs/063.

## Constitution check

- **I, II:** Identity only, layered. Pass.
- **III:** the password, the audit entry and the revocation run in one transaction. Pass.
- **IV:** the caller comes from the token. The kept session comes from the cookie, never the body. Pass.
- **V:** tests against real PostgreSQL, mutation checks, and an end-to-end run with two sessions. Pass.
