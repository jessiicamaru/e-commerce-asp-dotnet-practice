# Implementation Plan: Email confirmation

**Branch**: `063-email-confirmation` | **Spec**: [spec.md](spec.md) | **Issue**: #106

## Design

- **`users.EmailConfirmedAt`** (timestamptz, nullable). The migration adds it, then
  `UPDATE users SET "EmailConfirmedAt" = "CreatedAt"`. `DataInitializer` confirms the admin it seeds.
- **`email_confirmation_tokens`** has the same shape as `password_reset_tokens`: `Id`, `UserId`,
  `TokenHash char(64)` unique, `ExpiresAt`, `UsedAt`, `CreatedAt`, with a cascade from `users`.
- **`EmailConfirmations`** (Application, `Auth/Commands/EmailConfirmation/`):
  - `StageAsync(user, language)`. Deletes the account's unused links, then **stages** a new token and
    its `OutgoingEmail` through EF, so they commit with the caller's single save: registration's
    account, or the resend. `IOutgoingEmailRepository.Stage` adds the entity (`DataJson` is jsonb,
    written as a string).
  - `ConfirmEmailCommand(Token)`: in a transaction, `TryClaimAsync(hash, now)` returns the user id or
    null, then `TryConfirmAsync(userId, now)` runs the guarded UPDATE, then the audit entry.
  - `ResendConfirmationCommand(Language)`: signed in. Already confirmed → 409. Otherwise it locks the
    user's row and asks whether a token was made in the last minute (the pattern from specs/062). If so it
    returns silently; if not it stages a new link and saves.
- **Registration** (`RegisterCommandHandler`, `RegisterSellerCommandHandler`) calls `StageAsync` before
  its first save. The controller passes the request language, like forgot-password.
- **`AuthResponse`** gains `EmailConfirmed` (bool), filled by login, refresh and both registrations.
- **Shop applications:**
  - `EnsureMayApplyAsync` throws `ForbiddenException` with `code: EmailNotConfirmed` for an unconfirmed
    user.
  - Approval refuses an unconfirmed applicant with 409, before the guarded decision.
  - `ShopApplicationRow` and the staff response carry `ApplicantEmailConfirmed`.
- **Template** `EmailConfirmation` has vi and en words, a `/confirm-email?token=` link, and is in
  `ScrubbedOnceSent`.
- **Gateway:** `/api/auth/confirm-email` goes under `sign-in` (a guessable-token endpoint);
  `/api/auth/resend-confirmation` under `email`.
- **Storefront:**
  - `emailConfirmed` on the auth user, and `Auth.confirmEmail` / `Auth.resendConfirmation`;
  - a banner in the main layout while signed in and unconfirmed, with "Send it again";
  - a `/confirm-email` page that confirms and, when signed in, renews the session so the banner goes;
  - `/open-shop` says to confirm first instead of showing the form;
  - the admin shops page marks unconfirmed applicants.
- **Bruno:** the seller folder confirms the seller's address through Mailpit's API, the way a person
  would. Before that, approving the unconfirmed applicant is 409. Security checks: resend without a token
  is 401; confirming with a made-up token is 400.

## Decisions

1. **Existing accounts count as confirmed.** They predate the rule, and some already hold shops.
2. **Buying is not gated; selling is.** An unconfirmed address costs its owner nothing when paying. A shop
   is a public claim in the address's name.
3. **The approval is gated, not the seller registration.** Registering as a seller stays one step, and the
   application simply waits for the address, as the moderator also waits for it.
4. **24 hours, not 30 minutes.** A confirmation link grants nothing an attacker wants, and people open
   welcome emails late.

## Constitution check

- **I. Service Autonomy.** Everything is in Identity. Pass.
- **II. Clean Architecture.** Pass.
- **III. Atomic writes.** The token, the email and the account commit in one save, and a confirmation is
  guarded statements in one transaction. Pass.
- **IV. Identity from the token.** Resend reads the caller from `ICurrentUser`; confirm reads the account
  from the link's token, never from a body id. Pass.
- **V. Evidence.** Tests against real PostgreSQL, mutation checks, and an end-to-end run through Mailpit.
  Pass.
