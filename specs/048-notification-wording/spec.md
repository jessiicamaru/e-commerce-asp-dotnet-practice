# Feature Specification: Notification wording

**Feature Branch**: `048-notification-wording` | **Created**: 2026-09-24 | **Issue**: #119

## Why

Five kinds of notification reach people with holes in them. A seller whose product was refused reads
`“{{product}}” was not approved: {{reason}}`, and the same goes for a refused shop, an approved product,
a product taken down and a new review. The server sends the words. The storefront never passes them to
the sentence (`describeNotification` passes `order`, `total`, `amount`, `tracking`, `shop` and `by`, and
never `product`, `reason` or `rating`).

The deeper cause is that nothing ties what a service **sends** to what the storefront **reads**. A notice
is a kind and a bag of strings (specs/042). Specs/044, 045 and 046 each added kinds and data keys on the
server and strings on the client. The only tests covered the order kinds, and they check the client
against data the test itself made up.

## User Scenarios

### US1 - Every notice reads as a sentence (P1)

A seller opens the bell after a moderator refused their product. They read "“Fujifilm X-T5” was not
approved: the photos are blurred", in Vietnamese or English, whichever they use now. The same holds for
every kind any service can send.

**Acceptance**
1. Each of the 16 kinds renders in both languages with no `{{` left, and with the value of every key the
   server sends visible in the sentence.
2. "1 star" and "5 stars" in English.
3. A notice whose data lacks a value its sentence needs, such as one stored by an older service, reads
   "You have a new update." It never shows a sentence with a hole in it.

### US2 - A mismatch between server and storefront fails a test (P1)

A developer renames a data key on the server, adds a key or adds a kind, and forgets the storefront. A
test fails on the side they changed and names the kind and the key.

**Acceptance**
1. Server: every notice published in the existing notification tests carries exactly the keys declared
   for its kind: every required key, and none that is undeclared.
2. Server: the kinds in `NotificationKind` and the declared kinds are the same set.
3. Client: every declared kind has a sentence in both languages. Every declared key is either shown or
   deliberately consumed, such as `currency`. The client has no sentence for an undeclared kind.

## Requirements

- **FR-001**: The data keys of each notification kind are declared once, in one file both the server
  tests and the client tests read.
- **FR-002**: `describeNotification` fills every placeholder its sentences use from the notice's data.
- **FR-003**: A sentence that would have an empty placeholder falls back to the generic text.
- **FR-004**: The declaration is not enforced at run time. A wording mismatch must not fail the business
  transaction that sends the notice. The tests enforce it.

## Out of scope

- Translating the product name in a notice. The server sends the name as the seller wrote it, which is
  the product's default-language text.
- The notices #128 lists as missing (lock or ban, a delivery taken by the sweep, a hidden review).

## Assumptions

- The five kinds have sent these keys since the specs that introduced them, so stored notices already
  carry the data. Only their wording was broken. No backfill is needed.
