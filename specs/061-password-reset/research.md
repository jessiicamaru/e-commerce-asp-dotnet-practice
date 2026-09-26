# Phase 0 Research: Password reset

> Written on 2026-09-27, after the feature merged (#144), from the code at that merge, the pull request and
> [docs/features/email.md](../../docs/features/email.md) with
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-25

Six decisions. D1 to D3 are the plan's "Decisions", D4 and D5 are in the pull request, D6 is reconstructed from the
code. The project-level record is decision 45 in [docs/project/decisions.md](../../docs/project/decisions.md).

---

## D1 - SHA-256 of a 32-byte random token, not bcrypt

**Decision**: `ResetTokens.NewToken()` is 32 bytes from `RandomNumberGenerator`, base64url; the row stores
`ResetTokens.Hash(token)`, the lower-case hex SHA-256 (64 characters), under a unique index.

**Rationale**: It is 256 bits of randomness and cannot be guessed, so a slow hash adds nothing, and a fast one lets
a unique index find it. Storing only the hash means anyone reading the database cannot use a link.

**Alternatives considered**:

- **bcrypt/Argon2 like a password.** Rejected: a salted slow hash cannot be looked up by index, so the claim would
  have to scan candidate rows, and it defends against guessing a low-entropy secret, which this is not.
- **Store the token.** Rejected: a database read would then be a way into every account with a pending link.
- **A signed, stateless token (JWT-style).** Rejected: it cannot be made single-use or replaced without a row
  anyway.

---

## D2 - Queue the email directly, not through `IEmailSender`

**Decision**: `ForgotPasswordCommand`'s handler calls `IOutgoingEmailRepository.QueueAsync` inside the
transaction that stores the hash. It does not publish `EmailRequested`.

**Rationale**: Identity is both the requester and the sender (specs/060). Through `IEmailSender`, the token would
sit in an outbox message and a RabbitMQ queue - two more copies of a credential, outside Identity's table. The row
is the one copy.

**Alternatives considered**: `IEmailSender` like any other service - rejected for the copies above.

---

## D3 - Asking again deletes earlier unused links

**Decision**: `DeleteUnusedAsync(userId)` (an `ExecuteDelete` of the person's rows with `UsedAt IS NULL`) runs
before the new row is added.

**Rationale**: The newest link is the one the person just asked for. An older one still valid would be a second
way in, for anybody who can read an older email.

**Alternatives considered**: keep every link valid until it expires - rejected for the second way in; mark old ones
used - rejected as keeping rows that mean nothing.

---

## D4 - A sent reset email keeps no data

**Decision**: When `DispatchEmailsCommand` marks an email `Sent` and its template is in
`EmailTemplates.ScrubbedOnceSent` (at the merge, `PasswordReset` only), it sets `DataJson = "{}"`.

**Rationale**: Delivery needs the token; nothing afterwards does. A pending or failed row still holds it, but only
for as long as the link could work anyway.

**Alternatives considered**: delete the email row once sent - rejected: the row is the record that an email went,
and the audit and troubleshooting need it.

---

## D5 - One guarded claim, one 400 for every refusal

**Decision**: `TryClaimAsync` is `UPDATE password_reset_tokens SET "UsedAt" = now WHERE "TokenHash" = @hash AND
"UsedAt" IS NULL AND "ExpiresAt" > now RETURNING "UserId"`. No row is a `ValidationException` on `Token` with
"This link is invalid or has expired. Ask for a new one." The password, the audit entry and
`RevokeAllRefreshTokensAsync` run in the same transaction as the claim.

**Rationale**: Two submissions of one link at once serialise on the row; one gets the user id, the other nothing.
Used, expired, replaced and made-up tokens are all "not a valid link" to the person, and telling them apart would
tell an attacker which tokens once existed. Ending every session is the point of a reset when the account was
taken.

**Alternatives considered**: read, check, then update - rejected: two readers both see "unused" and both reset.

---

## D6 - The request's language, primary tag only

**Decision**: `AuthController.RequestLanguage()` takes the first `Accept-Language` entry's first two letters,
lower-cased ("en-US" is "en"); empty becomes Vietnamese in the handler, and a language with no words renders in
Vietnamese (specs/060).

**Rationale**: A person asking for a reset is not signed in, and has no order whose language to use; the
storefront sends `Accept-Language` on every request.

**Alternatives considered**: a stored language per person - it did not exist yet (it arrived with specs/083).
