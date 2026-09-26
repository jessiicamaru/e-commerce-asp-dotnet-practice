# Phase 0 Research: Change your password and your name

> Written on 2026-09-27, after the feature merged (#147), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-25

Five decisions. D1 to D3 are the plan's "Decisions"; D4 is in the pull request; D5 is reconstructed from the code.
The project-level record is decision 48 in [docs/project/decisions.md](../../docs/project/decisions.md).

---

## D1 - The current session is kept; the others end

**Decision**: After the new password is saved, `RevokeOtherRefreshTokensAsync(userId, keep, now)` revokes every
active refresh token of the user except `keep`, which the controller takes from the request's HttpOnly
`refreshToken` cookie. No cookie: every session ends.

**Rationale**: The person who just proved the password should not be signed out. Whoever else holds a session,
possibly the reason for the change, loses it. It is identified by the refresh cookie, which the server set and the
script cannot read - so a script cannot pick which session survives.

**Alternatives considered**:

- **End every session, this one included** (as a reset does). Rejected: the person is at the keyboard, signed in,
  and has just proved the password.
- **Take the session to keep from the body.** Rejected: a caller could name any token; the body is exactly where
  identity must not come from (constitution IV).

---

## D2 - A wrong current password counts against the sign-in pause

**Decision**: The handler asks `ISignInThrottle.BlockedUntilAsync` first (429 while paused) and calls
`RecordFailureAsync` on a wrong current password, the same per-email count as sign-in (specs/062). Success clears
it.

**Rationale**: Otherwise this endpoint is a way round specs/062 for anybody holding a stolen access token: it would
let them guess the password faster than the sign-in page allows. The gateway also puts `/api/auth/me/password`
under the `sign-in` per-IP policy.

**Alternatives considered**: a separate counter for this endpoint - rejected: two budgets are twice as many
guesses.

---

## D3 - No email change

**Decision**: `PUT /api/auth/me` changes first name, last name and phone only; the email is returned but never
written.

**Rationale**: An unconfirmed new address would undo specs/063: an account could confirm one address and then move
to somebody else's.

**Alternatives considered**: change it and mark it unconfirmed - rejected: it would also need the old address told,
a confirmation of the new one, and a rule for shops held under the old one; out of scope.

---

## D4 - The storefront renews the session after a name change

**Decision**: A successful details change calls `refreshSession()`.

**Rationale**: The header and the token's `given_name` (which signs reviews, specs/046) are read from the access
token; only a new token carries the new name.

**Alternatives considered**: update the name only in the client state - rejected: the next review would still be
signed with the old `given_name`.

---

## D5 - One transaction for the password; validation as registration

**Decision**: The new hash, the `PasswordChanged` entry (Security, no password) and the revocation run in one
`ExecuteInTransactionAsync`. `ChangePasswordCommandValidator` uses registration's minimum length and maximum bytes;
`UpdateMeCommandValidator` requires names of at most 100 characters and a phone of at most 20. `ProfileUpdated`
(User) carries the three fields before and after.

**Rationale**: A password change that saved without ending the other sessions would leave the reason for the change
signed in. Changing a password must not be a way round registration's rules.

**Alternatives considered**: save first, revoke after - rejected: a failure between them leaves the other sessions
alive with the new password in place.
