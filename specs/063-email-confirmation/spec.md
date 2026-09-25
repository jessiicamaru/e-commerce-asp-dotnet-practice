# Feature Specification: Email confirmation

**Feature Branch**: `063-email-confirmation` | **Created**: 2026-09-25 | **Issue**: #106

## Why

Anybody can register with somebody else's email address, and the system treats them as its owner. That
includes applying to open a shop in that name, and receiving that person's order confirmations and reset
links. Email exists since specs/060, so a confirmation link can be sent.

## User Scenarios

### US1 - A new account confirms its address (P1)

A person registers and gets an email: "Confirm your email address". Opening the link marks the address
confirmed. Until then a banner on every page says "Confirm your email: we sent a link to …" with
"Send it again".

**Acceptance**
1. Registering, as a customer or as a seller applicant, queues one `EmailConfirmation` email in the
   request's language, in the same transaction as the account. Its link is `/confirm-email?token=…`,
   works for **24 hours**, and works **once**.
2. `POST /api/auth/confirm-email {token}` is anonymous, because the link may be opened in another browser.
   It answers **204** and sets `users.EmailConfirmedAt`. A used, expired, replaced or unknown token gets
   **one 400**: "This link is invalid or has expired."
3. Two submissions of one link at once confirm once.
4. `emailConfirmed` is on every authentication response (register, login, refresh), so the storefront can
   draw the banner. It is for drawing, never for deciding, like `roles`.

### US2 - Send it again (P1)

**Acceptance**
1. `POST /api/auth/resend-confirmation` is **signed in**, and the account comes from the token. It answers
   **202** and sends a new link, which replaces the earlier one.
2. It sends at most one email a minute per account. Inside that minute it answers the same 202 and sends
   nothing. The gateway's `email` limit covers the route too.
3. An account already confirmed gets **409**.

### US3 - What an unconfirmed account may not do (P1)

Browsing, the cart and buying stay open: an unconfirmed address stops nobody paying. Opening a shop does
not, because a shop is a public claim made in the address's name.

**Acceptance**
1. A signed-in customer applying to sell with an unconfirmed address gets **403** with the fact
   `code: EmailNotConfirmed`.
2. Registering as a seller still creates the pending application, as it does today. **Approving** it
   while the applicant's address is unconfirmed gets **409**, "The applicant has not confirmed their email
   address yet". Staff see whether the address is confirmed on each application.

### US4 - Accounts from before this (P1)

Every account that exists when this ships counts as confirmed (`EmailConfirmedAt = CreatedAt`), and so
does the administrator seeded at startup. Asking every existing customer to confirm would stop sellers
who already have shops for no gain.

## Requirements

- **FR-001**: The token is 32 random bytes, sent base64url in the link and stored **only as its SHA-256
  hash**, the same as a reset link (specs/061). Its email is queued by Identity itself, and its data is
  scrubbed once sent.
- **FR-002**: Confirming is one guarded statement on the token (unused and unexpired) and one guarded
  statement on the user (`WHERE "EmailConfirmedAt" IS NULL`), in one transaction.
- **FR-003**: Both steps are audit entries, `EmailConfirmationSent` and `EmailConfirmed`, under User. They
  hold no token.
- **FR-004**: The migration only adds a nullable column and fills it for existing rows, so an earlier image
  still reads `users`.

## Out of scope

- Changing an email address (#104 covers the name and password only).
- Refusing orders from unconfirmed accounts.
