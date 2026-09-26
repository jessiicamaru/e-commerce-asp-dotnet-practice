# Feature Specification: Emails for what happens to people

> Completed on 2026-09-27, after the feature merged (#171), from the code at that merge, the pull request and
> docs/features/email.md.

**Feature Branch**: `083-more-emails` | **Created**: 2026-09-26 | **Status**: Merged (#171, 2026-09-26) | **Issue**: #167 (closes it)

**Input**: Issue #167 - what happens to a person reached them only in the bell. The issue left two options for the
language: store one on the account, or use the language of the request that caused the email.

## Why

Three emails existed: the order confirmation, the password reset and the address confirmation (specs/060, 061,
063). Everything else that happened to a person reached them only as a notice in the bell, which they see only if
they open the shop:
- their parcel shipped;
- their order was cancelled;
- a return was decided or refunded;
- a product they saved came back;
- their account was locked or banned. Signed out by it, they cannot open the bell at all.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A buyer hears about their order by email (Priority: P1)

A buyer's parcel ships; they receive "your order ... is on its way" with the shop and the tracking reference. If
their order is cancelled, or a return they asked for is accepted, refused or refunded, they receive an email saying
so - in the language the order was placed in.

**Why this priority**: These are the events a buyer is waiting for, and money or a parcel depends on each. The
issue's acceptance criterion is the shipped email arriving with its tracking reference.

**Independent Test**: Ship a parcel of a paid order; within the dispatcher's next sweep Mailpit holds one email to
the buyer whose subject says the order is on its way and whose body carries the tracking reference.

**Acceptance Scenarios**:

1. **Given** a paid order, **When** its seller (or the shop) ships the parcel, **Then** one `ParcelShipped` email
   is asked for the buyer, in the order's language, naming the shop (for a seller's parcel) and the tracking
   reference.
2. **Given** a parcel being prepared, **When** it is moved to preparing, **Then** no email is asked for.
3. **Given** a paid order, **When** the customer or staff cancels it, **Then** one `OrderCancelled` email is asked
   for, with the total refunded; **When** the cancellation is repeated, **Then** nothing more is asked for.
4. **Given** a return request, **When** it is accepted, refused (with a reason) or received, **Then** one
   `ReturnAccepted`, `ReturnRefused` or `ReturnRefunded` (with the amount) email is asked for; a refused attempt
   that does not move the return asks for nothing.

---

### User Story 2 - A locked or banned person learns why (Priority: P2)

A moderator locks an account until a date, or an administrator bans it. The person, now signed out, receives an
email saying so, with the reason and - for a lock - until when, in UTC.

**Why this priority**: The bell is the one place a signed-out person cannot look, so without an email they learn
nothing but a refused sign-in. Second because it is rarer than an order event.

**Independent Test**: Lock a customer for a day; one `AccountLocked` email is asked for, with `until` and `reason`,
in the language the person last used the shop in.

**Acceptance Scenarios**:

1. **Given** a customer, **When** they are locked, **Then** one `AccountLocked` email is asked for, with the end
   time written in UTC and saying so.
2. **Given** a customer, **When** they are banned, **Then** one `AccountBanned` email is asked for, with the reason.
3. **Given** a lock the moderation rules refuse, **When** it is attempted, **Then** no email is asked for.

---

### User Story 3 - A saved product coming back is emailed (Priority: P3)

A shopper saved a product that went out of stock. When it comes back, they receive an email saying so, in the
language they last used the shop in.

**Why this priority**: Useful, not urgent; it also forced the language decision, because no request of the
reader's causes it.

**Independent Test**: Save a product, let it go out of stock and come back; one `SavedBackInStock` email per saver
is asked for, and none while it merely stays in stock.

**Acceptance Scenarios**:

1. **Given** two shoppers saved a product, **When** its availability flips back to in stock, **Then** one email is
   asked for each, in `EmailTemplate.ReadersLanguage`.
2. **Given** a product off the shelf, **When** its stock comes back, **Then** nobody is emailed.

---

### User Story 4 - The emails speak the reader's language without a setting (Priority: P4)

A person who reads the shop in English receives English emails about their account and saved products, and after
switching the storefront to Vietnamese, receives Vietnamese ones within minutes - without finding any setting.

**Why this priority**: It is what makes stories 2 and 3 readable. Last because it has a safe default (Vietnamese).

**Independent Test**: Sign in with `Accept-Language: en`; `users.Language` is `en`. Renew the session with
`Accept-Language: vi-VN`; it is `vi`. Renew with `Accept-Language: tlh`; it is unchanged.

**Acceptance Scenarios**:

1. **Given** a new account registered with `Accept-Language: en-US`, **When** it is created, **Then**
   `users.Language` is `en`.
2. **Given** a person, **When** they sign in or renew their session with a supported language, **Then** it is
   recorded; **When** the header names an unsupported one, **Then** nothing changes.
3. **Given** an email asked for in `ReadersLanguage` for a person who never said, **When** Identity queues it,
   **Then** it is written in Vietnamese.

---

### Edge Cases

- **A change that did not happen.** A refused move, a repeated cancellation, a product off the shelf coming back:
  none asks for an email, because each email is asked for inside the transaction of the change it describes.
- **A sender that cannot know the language.** Catalog does not know who a saver is; it asks for
  `ReadersLanguage` and Identity fills it in.
- **A header like `en-US`.** Counts as `en`. A language no email is written in records nothing.
- **Somebody who never signs in again.** Keeps the language they last used; noted as a known limit.
- **The product name in a back-in-stock email.** The default-language name, as the notice uses.
- **Emails to sellers.** None: sellers read the console and the bell (out of scope).
- **A redelivered request.** Identity keeps each request once by its `EmailId` (specs/060), unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** Eight new emails, each with built-in Vietnamese and English words, declared placeholders, sample data
  for the console's preview, and editable at `/admin/emails` like the first three (specs/077):

  | Email | Placeholders |
  | :-- | :-- |
  | `ParcelShipped` | `{name}`, `{order}`, `{shop}`, `{tracking}`, `{link}` |
  | `OrderCancelled` | `{name}`, `{order}`, `{total}`, `{link}` |
  | `ReturnAccepted` | `{name}`, `{order}`, `{link}` |
  | `ReturnRefused` | `{name}`, `{order}`, `{reason}`, `{link}` |
  | `ReturnRefunded` | `{name}`, `{order}`, `{amount}`, `{link}` |
  | `SavedBackInStock` | `{name}`, `{product}`, `{link}` |
  | `AccountLocked` | `{name}`, `{until}`, `{reason}` |
  | `AccountBanned` | `{name}`, `{reason}` |

- **FR-002** Each is asked for **where the notice is sent, in the same transaction**, through the outbox
  (`IEmailSender`). A change that did not happen asks for nothing: a refused move, a repeated cancellation, a
  product off the shelf coming back.
- **FR-003** **Language.**
  - An email about an order is written in the order's language (specs/021).
  - The others are written in **the language the person last used the shop in**. Identity records it as
    `users.Language` at sign-up, at sign-in and at each session renewal, from `Accept-Language`.
  - A sender that cannot know the language asks for `EmailTemplate.ReadersLanguage`, the empty string. Identity
    fills it in when it queues the email, falling back to Vietnamese. Catalog is such a sender: it does not know
    who a saver is.
  - Only a language an email is written in is recorded. "en-US" counts as "en"; anything else changes nothing.
- **FR-004** None of the eight is a required-link template: an administrator may edit any placeholder out
  (`RequiredOf` is empty for all eight), unlike the reset and confirmation emails.

### Key Entities

- **Email template**: a name (`EmailTemplate.*` in Shared) with words per language, placeholders, sample data, in
  Identity's `EmailTemplates`.
- **Account language** (`users.Language`): the language the person last used the shop in; null until they say.
- **Email request** (`EmailRequested`): unchanged record; eight new template values and the empty `Language` meaning
  "the reader's".

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Shipping a parcel delivers the buyer's "is on its way" email with the tracking reference, read back
  from Mailpit by Bruno against the rebuilt stack (the issue's acceptance criterion).
- **SC-002**: Each of the eight events asks for exactly one email, and each refused or repeated change asks for
  none (the Order, Catalog and Identity tests listed in the pull request).
- **SC-003**: All 11 templates have words in both languages and render their sample data whole; the email list
  asserts all 22 entries.
- **SC-004**: The language is learnt at sign-up, sign-in and renewal, and an unsupported header changes nothing.
- **SC-005**: Four mutations each turned their tests red (from the pull request).

## Assumptions

- Vietnamese and English are the only languages an email is written in (specs/021).
- The storefront sends `Accept-Language` on every request and renews the session every few minutes, so a language
  switch reaches the account without a setting.
- These emails are transactional; there is no opt-out.

## Decisions

- **The account learns its language; there is no setting.** A setting is a screen nobody visits. The storefront
  already sends `Accept-Language` on every request, and a renewal every few minutes carries a switch to the
  emails. It costs one guarded `UPDATE`, which writes nothing when the value is unchanged.
- **Returns are three emails, not one "decided" email.** The issue named a single `ReturnDecided`. An editable
  template with a condition inside it cannot be edited sensibly, so accepted, refused and refunded are separate,
  each with its own words.
- **The cancellation email says the whole total is refunded**, which is what specs/039 does. It does not say who
  cancelled, so the words need no per-actor wording.
- **A lock's end time is written in UTC and says so.** An email has no reader's time zone.
- **No opting out.** These are transactional. Back in stock stops when the product is no longer saved.

The full reasoning, with rejected alternatives, is in [research.md](./research.md).

## Out of scope

- A language setting on the account page.
- Emails to sellers (a new sale, a payout): they read the shop console daily.
- Product names in the saver's language: the email uses the product's default-language name, as the notice does.
