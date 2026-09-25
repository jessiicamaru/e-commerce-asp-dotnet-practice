# Implementation Plan: Password reset

**Branch**: `061-password-reset` | **Spec**: [spec.md](spec.md) | **Issue**: #103

## Design

**Identity**

*Domain.* `PasswordResetToken`, stored in `password_reset_tokens`:

| Column | Notes |
| :-- | :-- |
| `Id` | |
| `UserId` | foreign key to users, cascade |
| `TokenHash` | char(64), unique |
| `ExpiresAt` | |
| `UsedAt` | null until used |
| `CreatedAt` | |

*Application* (`Auth/Commands/PasswordReset/`):
- `ForgotPasswordCommand(Email)`, with a `Language` the controller fills from `Accept-Language`:
  - it runs in one transaction and always completes normally;
  - for a real account: delete that account's unused tokens, insert the new one, record
    `PasswordResetRequested`, and queue the `PasswordReset` email with `{ token }`, all saved together.
- `ResetPasswordCommand(Token, Password)`:
  - the validator uses registration's password rules;
  - the handler runs `TryClaimAsync(hash, now)`, which returns a user id or none. None is a
    `ValidationException` on `Token`, so a 400 with a message.
  - After a claim: set the password hash, then `RevokeAllRefreshTokensAsync`, record `PasswordReset`,
    and save, in one transaction.
- `EmailTemplates.PasswordReset`, in vi and en; the link is `{StorefrontUrl}/reset-password?token=…`.
- The dispatcher: once a `PasswordReset` email is sent, its `DataJson` is replaced with `{}`.

*WebApi.* `AuthController` gains `forgot-password` (202) and `reset-password` (204), both anonymous.

**Storefront**
- `services/auth`: `forgotPassword(email)`, `resetPassword(token, password)`.
- The sign-in page links to `/forgot-password`.
- `pages/forgot-password` shows the same confirmation whatever the answer, except that a network
  failure shows the generic error.
- `pages/reset-password` reads `?token`, takes the new password twice, and on success goes to sign-in
  with a "password changed" note. It shows a 400's message, and has a link to ask again.
- Locales: `auth.json`, vi and en.

**Bruno.** `auth/forgot password is 202 for anybody.yml` and `security-checks/reset with a bad token is 400.yml`.

## Decisions

- **SHA-256, not bcrypt, for the token.** It is 256 bits of randomness and cannot be guessed, so a
  slow hash adds nothing, and a fast one lets a unique index find it.
- **Queue the email directly, not through `IEmailSender`.** The token would otherwise sit in an outbox
  message and a broker queue. The row is the one copy, and it is scrubbed after sending.
- **Deleting earlier unused tokens.** The newest link is the one the person just asked for. An older
  one still valid would be a second way in.

## Constitution check

- III: the token, its email and its audit entry commit together. The claim is one guarded statement.
  Pass.
- IV: the reset is anonymous by nature; the token is the identity, and it is single-use. Pass.
- V: tests fail first, mutation checks follow, and there is an end-to-end run through Mailpit. Pass.
- Migration: a new table only.
