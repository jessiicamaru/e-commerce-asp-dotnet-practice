# Feature Specification: A mistyped tracking reference can be corrected

**Feature Branch**: `105-correct-tracking` | **Created**: 2026-09-27 | **Issue**: #212

**Status**: Draft

**Input**: Issue #212 - "a mistyped tracking reference can never be corrected".

## Why

A seller or administrator who types the wrong tracking reference when shipping is stuck with it. The buyer's link goes
to somebody else's parcel, or nowhere. `docs/features/fulfilment-and-delivery.md`: "Shipping again with a different
tracking reference is 409", and no route changes a shipped part's reference.

## User Scenarios & Testing *(mandatory)*

### US1 - The sender corrects the reference while the parcel is on its way (Priority: P1)

A seller corrects the reference of their own shipped part, and an administrator corrects the shop's own part's, until
the parcel is delivered. The buyer is told the new reference, and the audit log records the old and the new ones.

**Why this priority**: This is the issue.

**Independent Test**: Ship a part as "VN-1234", correct it to "VN-1243". The buyer's order shows "VN-1243", the buyer
holds a `TrackingCorrected` notice, and the audit entry shows both references.

**Acceptance Scenarios**:

1. **Given** a shipped, undelivered part, **When** its seller corrects the reference, **Then** the part (and the
   order's own reference, when it has one part) reads the new one.
2. **Given** the same reference again, **Then** 200 and nothing changes: no notice and no audit entry.
3. **Given** a delivered part, **Then** 409, because there is nothing left to track.
4. **Given** a part not yet shipped, or one cancelled (specs/104), **Then** 409.
5. **Given** another seller's part, **Then** 404, the one "Sale not found." (specs/034).
6. **Given** an empty reference or one over 100 characters, **Then** 400.

### Edge Cases

- **The delivery clock.** `ShippedAt` does not move. Automatic delivery still counts 7 days from when the parcel
  actually left (specs/040), so a correction cannot restart it and delay the seller's money or the buyer's return
  window.
- **Repeated corrections.** There is no hard limit. Every one is audited with the old and new reference and every one
  tells the buyer, so a seller "hiding" a parcel by rewriting it leaves a trail the buyer sees.
- **Two corrections at once** serialise under the order's row lock, and the last one stands.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `PUT /api/orders/sales/{id}/tracking` (Seller) and `PUT /api/orders/fulfilment/{id}/tracking` (Admin, the
  shop's own part), body `{ "trackingReference" }` (1-100 characters).
- **FR-002**: Under the order's row lock, one guarded `UPDATE ... WHERE "Status" = 'Shipped' AND "DeliveredAt" IS NULL
  AND "CancelledAt" IS NULL` sets the new reference. The order's summary reference is rewritten, and `ShippedAt` is
  untouched.
- **FR-003**: In the same transaction: the audit entry `TrackingCorrected` (before and after), and the notice
  `TrackingCorrected` to the buyer, with data `orderId`, `tracking` and optionally `shop`, declared in
  `notification-kinds.json`.
- **FR-004**: The storefront offers "Correct" beside a shipped, undelivered reference on the seller's sale page and the
  admin order page.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Order tests cover:
  - a correction shows on the buyer's order and the sale, and on the order when it has one part;
  - the buyer is notified and the audit entry holds both references;
  - the same reference is a no-op;
  - delivered, not shipped and cancelled parts are 409; another's is 404;
  - `ShippedAt` does not move.
- **SC-002**: Storefront tests: the seller's dialog sends the new reference; no "Correct" once delivered.
- **SC-003**: Bruno: correcting on a delivered order is 409, correcting another seller's part is 404, and without a
  token is 401.
- **SC-004**: Mutations make tests fail: the delivered guard removed; `ShippedAt` reset.

## Decision

1. **Until delivered, not after** ([research.md](research.md) D1).
2. **No limit; a trail instead** (D2).
3. **The shipping time stands** (D3).

## Assumptions

- The carrier's reference is all that identifies a parcel. There is no carrier integration to validate it against.

## Out of scope

- Emailing the corrected reference (the buyer is notified in the app); changing the carrier.
