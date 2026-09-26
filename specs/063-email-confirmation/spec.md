# Feature Specification: Email confirmation

> Completed on 2026-09-27, after the feature merged (#146), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md) with
> [docs/features/email.md](../../docs/features/email.md) and [docs/features/marketplace.md](../../docs/features/marketplace.md).

**Feature Branch**: `063-email-confirmation` | **Created**: 2026-09-25 | **Issue**: #106

**Status**: Merged (#146, 2026-09-25).

**Input**: Issue #106: an account is treated as the owner of whatever address it registered with.

## Why

Anybody can register with somebody else's email address, and the system treats them as its owner. That
includes applying to open a shop in that name, and receiving that person's order confirmations and reset
links. Email exists since specs/060, so a confirmation link can be sent.

## User Scenarios & Testing *(mandatory)*

### US1 - A new account confirms its address (Priority: P1)

A person registers and gets an email: "Confirm your email address". Opening the link marks the address
confirmed. Until then a banner on every page says "Confirm your email: we sent a link to …" with
"Send it again".

**Why this priority**: Everything else in this feature asks "is the address confirmed?"; this is how it becomes so.

**Independent Test**: Register, read the link from Mailpit, post it to `/api/auth/confirm-email`: 204, and the next
sign-in says `emailConfirmed: true`.

**Acceptance** (as first written)
1. Registering, as a customer or as a seller applicant, queues one `EmailConfirmation` email in the
   request's language, in the same transaction as the account. Its link is `/confirm-email?token=…`,
   works for **24 hours**, and works **once**.
2. `POST /api/auth/confirm-email {token}` is anonymous, because the link may be opened in another browser.
   It answers **204** and sets `users.EmailConfirmedAt`. A used, expired, replaced or unknown token gets
   **one 400**: "This link is invalid or has expired."
3. Two submissions of one link at once confirm once.
4. `emailConfirmed` is on every authentication response (register, login, refresh), so the storefront can
   draw the banner. It is for drawing, never for deciding, like `roles`.

**Acceptance Scenarios**:

1. **Given** a new registration with `Accept-Language: en`, **When** it succeeds, **Then** the response says
   `emailConfirmed: false`, one English "Confirm your email address" email is queued with the account, and one
   `email_confirmation_tokens` row holds a hash expiring in 24 hours.
2. **Given** that link, **When** it is posted, **Then** 204, `users.EmailConfirmedAt` is set, and sign-in and
   refresh say `emailConfirmed: true`.
3. **Given** the same link again, or one past 24 hours, or a made-up one, **When** it is posted, **Then** 400 on
   `Token`: "This link is invalid or has expired."
4. **Given** one link, **When** two submissions arrive at once, **Then** it is confirmed once and one
   `EmailConfirmed` entry is recorded.
5. **Given** a signed-in session on the storefront, **When** the confirm page succeeds, **Then** the session is
   renewed and the banner goes.

---

### US2 - Send it again (Priority: P1)

**Why this priority**: Links expire and emails get lost; without a way to ask again an account could never become
able to sell.

**Independent Test**: Signed in and unconfirmed, `POST /api/auth/resend-confirmation`: 202 and a new email; the
earlier link no longer works.

**Acceptance** (as first written)
1. `POST /api/auth/resend-confirmation` is **signed in**, and the account comes from the token. It answers
   **202** and sends a new link, which replaces the earlier one.
2. It sends at most one email a minute per account. Inside that minute it answers the same 202 and sends
   nothing. The gateway's `email` limit covers the route too.
3. An account already confirmed gets **409**.

**Acceptance Scenarios**:

1. **Given** an unconfirmed account whose link is more than a minute old, **When** it asks again, **Then** 202, a
   new link is sent and the earlier one is deleted.
2. **Given** a link sent less than a minute ago, **When** it asks again - even five times at once - **Then** 202
   each time and no email is sent.
3. **Given** a confirmed account, **When** it asks, **Then** 409 "This email address is already confirmed."
4. **Given** no access token, **When** the endpoint is called, **Then** 401.

---

### US3 - What an unconfirmed account may not do (Priority: P1)

Browsing, the cart and buying stay open: an unconfirmed address stops nobody paying. Opening a shop does
not, because a shop is a public claim made in the address's name.

**Why this priority**: Selling in somebody else's name is the harm #106 names; the confirmation is only worth
having if it gates that.

**Independent Test**: An unconfirmed customer applies to sell: 403 `EmailNotConfirmed`. A seller registration's
application cannot be approved (409) until the applicant confirms, then it can (200).

**Acceptance** (as first written)
1. A signed-in customer applying to sell with an unconfirmed address gets **403** with the fact
   `code: EmailNotConfirmed`.
2. Registering as a seller still creates the pending application, as it does today. **Approving** it
   while the applicant's address is unconfirmed gets **409**, "The applicant has not confirmed their email
   address yet". Staff see whether the address is confirmed on each application.

**Acceptance Scenarios**:

1. **Given** an unconfirmed customer, **When** they `POST /api/shop-applications`, **Then** 403 with
   `code: EmailNotConfirmed` and "Confirm your email address before applying to sell."
2. **Given** a pending application from an unconfirmed applicant, **When** staff approve it, **Then** 409 and
   nothing changes; the staff list shows `applicantEmailConfirmed: false`.
3. **Given** the applicant confirms, **When** staff approve, **Then** 200 and the shop opens.
4. **Given** the storefront's `/open-shop` for an unconfirmed person, **When** it opens, **Then** it asks them to
   confirm first instead of showing the form.

---

### US4 - Accounts from before this (Priority: P1)

Every account that exists when this ships counts as confirmed (`EmailConfirmedAt = CreatedAt`), and so
does the administrator seeded at startup. Asking every existing customer to confirm would stop sellers
who already have shops for no gain.

**Why this priority**: Without it, the release would stop every existing shop from being approved and put a banner
in front of every existing customer.

**Independent Test**: After the migration, `SELECT count(*) FROM users WHERE "EmailConfirmedAt" IS NULL` counts
only accounts created since.

**Acceptance Scenarios**:

1. **Given** accounts from before the migration, **When** it runs, **Then** each has `EmailConfirmedAt =
   CreatedAt` (32 of 32 on the development database).
2. **Given** the first administrator seeded at startup, **When** it is created, **Then** it is confirmed.

---

### Edge Cases

- **A link opened after the address was confirmed another way** (an older link, a resend): the link is spent and
  nothing changes - 204.
- **A link opened in another browser** is anonymous, so it works; only the signed-in session in the same browser is
  renewed.
- **A resend while a previous email is still pending**: the old link is deleted; its email, if it arrives, carries a
  link that no longer works.
- **An older storefront reading an auth response without `emailConfirmed`** treats the missing value as `true`.
- **Buying while unconfirmed** is allowed, and so are order confirmations to that address.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The token is 32 random bytes, sent base64url in the link and stored **only as its SHA-256
  hash**, the same as a reset link (specs/061). Its email is queued by Identity itself, and its data is
  scrubbed once sent.
- **FR-002**: Confirming is one guarded statement on the token (unused and unexpired) and one guarded
  statement on the user (`WHERE "EmailConfirmedAt" IS NULL`), in one transaction.
- **FR-003**: Both steps are audit entries, `EmailConfirmationSent` and `EmailConfirmed`, under User. They
  hold no token.
- **FR-004**: The migration only adds a nullable column and fills it for existing rows, so an earlier image
  still reads `users`.
- **FR-005**: Registration (both kinds) MUST stage the link and its email in the account's own save.
- **FR-006**: Resending MUST be signed in, MUST replace the earlier link, and MUST send at most one email a minute
  per account; a confirmed account gets 409.
- **FR-007**: Applying to sell MUST require a confirmed address (403 `EmailNotConfirmed`); approving MUST wait for
  it (409); staff MUST see the flag.
- **FR-008**: Every authentication response MUST carry `emailConfirmed`.

### Key Entities

- **Confirmation** (`users.EmailConfirmedAt`): when the address was shown to belong to the person; null until then.
- **Confirmation token** (`email_confirmation_tokens`): whose, the hash, expiry, when used.
- **Confirmation email** (`outgoing_emails`, template `EmailConfirmation`): the one row holding the token until sent.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: No shop is approved for an address its applicant has not confirmed.
- **SC-002**: A new account can confirm within 24 hours from one email, and ask for another at most once a minute.
- **SC-003**: 100% of accounts existing at release count as confirmed, and no existing shop is affected.
- **SC-004**: No token appears in an audit entry or in a sent email's row.

## Assumptions

- Email delivery (specs/060) and Mailpit in development; the storefront at `Email:StorefrontUrl`.
- The gateway's `sign-in` and `email` limits (specs/062) protect the two new routes.

## Out of scope

- Changing an email address (#104 covers the name and password only).
- Refusing orders from unconfirmed accounts.
