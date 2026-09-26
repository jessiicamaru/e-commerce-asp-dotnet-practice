# Feature Specification: The notices nobody got

> Completed on 2026-09-27, after the feature merged (#142), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Feature Branch**: `059-missing-notices` | **Created**: 2026-09-24 | **Issue**: #128 (part B of two)

**Status**: Merged (#142, 2026-09-24). Closes #128; part A was [specs/058](../058-audit-gaps/) (#141).

**Input**: Issue #128, part B: "some events tell nobody".

## Why

Specs/042 tells people what happened to them. Three events told nobody:

- **An account locked or banned.** The person learns why only by trying to sign in (specs/049). Once a
  lock has run out, their notifications show nothing about it.
- **A parcel taken as delivered by the 7-day sweep** (specs/040). Its seller is told when the
  **customer** confirms, and not at all when the sweep does. Yet that moment decides when their money
  becomes due.
- **A review hidden by a moderator.** Its author never learns why it disappeared.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - People hear about what staff did to them (Priority: P1)

A person whose account was locked or banned, or whose review was hidden, finds a notice saying so and why.

**Why this priority**: These are the decisions a person most needs explained, and the only ones made about them
by somebody else without their involvement. A hidden review simply vanishes; a lock that has run out leaves no
trace at all.

**Independent Test**: As staff, lock a customer for two days, ban another, hide a third person's review; each
person's `GET /api/notifications` holds one notice of the matching kind with the reason.

**Acceptance** (as first written)
1. Locking an account sends `AccountLocked` to its owner, with the end time and the reason. The storefront
   shows the time in the reader's language and time zone.
2. Banning sends `AccountBanned`, with the reason.
3. Hiding a review sends `ReviewHidden` to its author, with the product and the reason, linked to the
   product.

**Acceptance Scenarios**:

1. **Given** a customer, **When** a moderator locks the account for 2 days with the reason "Cooling off",
   **Then** the customer has one `AccountLocked` notice whose `until` is the lock's end in UTC (ISO 8601) and
   whose `reason` is "Cooling off".
2. **Given** that notice, **When** it is read in English or Vietnamese, **Then** the end is shown as a local
   date and time in that language, never as the stored `...Z` text.
3. **Given** a customer, **When** an administrator bans them with the reason "Fraud", **Then** they have one
   `AccountBanned` notice with that reason.
4. **Given** a visible review, **When** staff hide it with the reason "Advertising", **Then** its author has one
   `ReviewHidden` notice naming the product and the reason, linked to `/products/{id}`.
5. **Given** a review already hidden, **When** staff try to hide it again, **Then** the second attempt is a 409
   and no second notice is sent.

---

### User Story 2 - A seller hears when the sweep delivers their parcel (Priority: P1)

A seller learns that a parcel of theirs counts as delivered even when the customer never said so, because a week
passed after it shipped.

**Why this priority**: That moment starts the clock on the seller's money (specs/040, later specs/066). A seller
told only about customer confirmations cannot tell a silent parcel from a lost one.

**Independent Test**: Ship a seller's parcel, move its `ShippedAt` more than 7 days back, run the sweep; the
seller has one `ParcelAutoDelivered` notice and no `ParcelReceived`.

**Acceptance** (as first written)
1. A seller's parcel taken as delivered by the sweep sends `ParcelAutoDelivered` to that seller. It is
   worded differently from a customer's confirmation, because nobody clicked.
2. The shop's own parcels tell nobody, as for `ParcelReceived`.

**Acceptance Scenarios**:

1. **Given** a seller's parcel shipped more than 7 days ago and not confirmed, **When** the sweep runs,
   **Then** the seller has one `ParcelAutoDelivered` notice with the order id, linked to `/shop/sales/{orderId}`,
   and no `ParcelReceived` is sent.
2. **Given** the shop's own parcel in the same situation, **When** the sweep runs, **Then** no notice is sent.
3. **Given** the sweep's transaction rolls back, **When** it is retried, **Then** no notice from the failed
   attempt exists, because the notice was staged in that transaction.

---

### User Story 3 - The storefront has words for all of them (Priority: P1)

Each new kind is declared in `notification-kinds.json` (specs/048), with a sentence in Vietnamese and
English. The contract tests on both sides cover them.

**Why this priority**: A notice the storefront cannot word reads "You have a new update", which tells the person
nothing. Declaring the kinds once keeps the server and the storefront from disagreeing about the keys.

**Independent Test**: `cd client && npm test -- src/utils/notifications` passes, with every declared kind worded
in both languages and no `{{` left in any sentence.

**Acceptance Scenarios**:

1. **Given** the four new kinds declared with their keys, **When** the storefront's contract test runs, **Then**
   each has a sentence in `en` and `vi` that fills every placeholder.
2. **Given** a server test that publishes one of these notices, **When** it checks the notice against the
   declaration (`NotificationContract.Problems`), **Then** no key is missing.

---

### Edge Cases

- **A locked person cannot read the notice while locked.** Sign-in already shows the reason (specs/049); the
  notice is the record once the lock ends (see Decision).
- **A ban lifted later.** The `AccountBanned` notice remains as the record.
- **The shop's own parcel** has no seller to tell.
- **Two moderators hiding one review at once.** One guarded statement decides (specs/057); the notice is staged in
  its callback, so only the winner tells the author.
- **A hidden review whose product cannot be found.** The notice carries an empty product name rather than
  failing the hide (the handler uses `product?.Name ?? ""`); the storefront, finding a hole, shows its generic
  "You have a new update" rather than a sentence with a gap (specs/048).
- **A lock's end in the reader's time zone.** The stored value is UTC; formatting happens in the browser.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Locking an account MUST send `AccountLocked` to its owner with `until` (the lock's end, ISO 8601
  UTC) and `reason`.
- **FR-002**: Banning an account MUST send `AccountBanned` to its owner with `reason`.
- **FR-003**: Hiding a review MUST send `ReviewHidden` to its author with `product` and `reason`, linked to the
  product page.
- **FR-004**: The delivery sweep MUST send `ParcelAutoDelivered` with `orderId` to the seller of each parcel it
  takes as delivered, linked to the sale; it MUST NOT send `ParcelReceived`; the shop's own parcels tell nobody.
- **FR-005**: Every notice MUST be staged in the same transaction as the change it announces.
- **FR-006**: Each new kind and its required keys MUST be declared in `notification-kinds.json`.
- **FR-007**: The storefront MUST word each new kind in Vietnamese and English, and MUST show a lock's end in the
  reader's language and time zone.

### Key Entities

- **Notification kind**: a name plus the data keys it requires, declared once in
  `Ecommerce.Shared/Notifications/notification-kinds.json` and mirrored by `NotificationKind` constants.
- **Notice** (`UserNotificationRequested`, kept by Activity in `notifications`): recipient, kind, data, link.
  Unchanged; four new kinds use it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Each of the four events produces exactly one notice to the right person, asserted by a server test
  that failed before the change.
- **SC-002**: The storefront's contract test covers all four kinds in both languages (it failed first with 9 red
  cases).
- **SC-003**: A lock's end is never shown as raw UTC, asserted by a storefront test.
- **SC-004**: The suites pass: Order 184/184, Identity 83/83, Catalog 161/161 against real PostgreSQL; storefront
  291/291 with lint and `tsc` clean (figures from the pull request).

## Assumptions

- `INotifier`, Activity's idempotent insert and the storefront's `describeNotification` from specs/042 and 048
  are correct; this feature adds kinds and callers.
- The guarded hide of specs/057 (#140) and the sweep's `stage` callback of specs/040 are where a notice can be
  staged inside the deciding transaction.

## Out of Scope

- Telling the person when a lock is lifted or a ban removed.
- Email for any of these events (email arrives in [specs/060](../060-email/)).
- The missing audit entries - part A, [specs/058](../058-audit-gaps/).

## Decision

**Notify on the lock, not only on its end.** A locked person cannot read the notice while locked. After
the lock ends, though, the notice is their record of what happened and why, and the reason is already
shown at sign-in in the meantime. A ban is the same, if it is ever lifted.
