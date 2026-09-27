# Feature Specification: A seller cancels the part of an order they cannot fulfil

**Feature Branch**: `104-seller-cancels-part` | **Created**: 2026-09-27 | **Issue**: #211

**Status**: Merged (#224, 2026-09-27)

**Input**: Issue #211 - "a seller cannot cancel the part of an order they cannot fulfil".

## Why

A seller whose item turns out to be out of stock, damaged or lost cannot say so. Their parcel waits until an
administrator cancels the **whole** order, taking every other seller's goods with it. Only two cancel routes exist:
the customer's, while every parcel is still waiting, and staff's, for the whole order (specs/039).
`docs/features/fulfilment-and-delivery.md`: "Cancellation is whole-order only."

## User Scenarios & Testing *(mandatory)*

### US1 - A seller cancels their part, and the rest of the order goes on (Priority: P1)

A seller cancels their own part of a paid order while it is waiting or being prepared, and gives a reason. The buyer
is told the reason and is refunded what they paid for that part's goods and tax. Its stock goes back on the shelf, the
seller is owed nothing for it, and the other sellers ship their parts as usual.

**Why this priority**: This is the issue.

**Independent Test**: A two-seller order. Seller A cancels their part; seller B ships theirs. The buyer is refunded
exactly A's lines, A's stock is back, and the order ends `Shipped` with one part cancelled.

**Acceptance Scenarios**:

1. **Given** a paid order with parts from A and B, **When** A cancels theirs with a reason, **Then** the part reads
   cancelled with the reason, the buyer is notified, Payment records a refund of A's lines (goods less their
   discounts, plus their tax), and Inventory returns A's units.
2. **Given** that, **When** B ships, **Then** the order reads `Shipped`, because a cancelled part is not waited for.
3. **Given** a part already shipped, **Then** cancelling it is 409.
4. **Given** another seller's part, or somebody else's order, **Then** 404, the same answer as for a sale that is not
   yours.
5. **Given** the same cancellation sent twice, **Then** one refund and one restock (idempotent).

---

### US2 - Cancelling the last part is cancelling the order (Priority: P1)

When the part being cancelled is the only one left (a single-seller order, or every other part is already
cancelled), the order itself is cancelled, exactly as a whole-order cancellation is (specs/039). The status becomes
`Cancelled`, every voucher use is given back, and the refund is everything charged less what earlier part refunds
already returned.

**Why this priority**: Without it, a single-seller order would sit `Paid` forever with nothing left to ship. It would
also refund the goods and keep the delivery charge for a delivery that will never happen.

**Acceptance Scenarios**:

1. **Given** a one-seller order, **When** its seller cancels their part, **Then** the order is `Cancelled` (by
   "Seller") and the refund is the full amount charged.
2. **Given** a two-seller order where A cancelled first, **When** B cancels too, **Then** the order is `Cancelled`
   and the total refunded across both refunds equals what was charged, never more.

---

### US3 - Staff cancel the shop's own part (Priority: P2)

An administrator cancels the shop's own part of an order, with a reason, under the same rules.

**Why this priority**: The shop sells too (a `null` seller). An administrator can already cancel the whole order;
this gives them the narrower tool.

### Edge Cases

- **The whole order afterwards.** After a part is cancelled, the customer or staff can still cancel the rest. The
  refund is what is left, never the full amount a second time (Payment subtracts earlier part refunds).
- **A cancellation that overtakes the completion.** The order is `Paid` in Order, but Inventory may not have
  confirmed yet. The part's reservations are released if `Held` and put back if `Confirmed`, as in specs/039.
- **Vouchers.** The cancelled seller's own shop voucher on this order is given back, since it applied only to their
  lines, which are gone. A platform voucher and free delivery stay used while the order goes on.
- **Delivery.** It is charged per order, and the order still ships, so a part cancellation does not refund it. The
  last part (US2) refunds everything that is left.
- **Money and insights.** A cancelled part is never on the way, due or paid out. Admin and seller insights leave it
  out, as they leave out a received return (specs/084).
- **Rollback.** An image from before this ignores the new columns, and could ship a part cancelled after it was
  deployed. Recorded as accepted, the same trade as specs/040.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `POST /api/orders/sales/{orderId}/cancel` (Seller), body `{ "reason" }` (1-500 characters). It cancels
  the caller's part while it is `Pending` or `Preparing` and the order is paid. A part already cancelled is answered
  200, a no-op.
- **FR-002**: `POST /api/orders/fulfilment/{orderId}/shop-part/cancel` (Admin), the same rules for the shop's own part.
- **FR-003**: In one transaction, under the order's row lock (the lock every parcel move takes):
  - set `order_shipments.CancelledAt`, `CancelReason`, `CancelledBy` and `CancelRefund` on the part, with a guarded
    `UPDATE ... WHERE "CancelledAt" IS NULL AND "Status" IN ('Pending','Preparing')`;
  - if other parts remain, stage `OrderPartCancelledEvent`, release that seller's shop voucher, audit, and notify the
    buyer;
  - if none remains, run the whole-order cancellation of specs/039 (`OrderCancelledEvent`, by "Seller" or "Staff").
- **FR-004**: Inventory's `RestockCancelledPartConsumer` returns the part's variants from the order's reservations
  (`Held` released, `Confirmed` put back), once, and announces availability.
- **FR-005**: Payment's `RefundCancelledPartConsumer` records a refund of the event's amount, unique on
  `refunds.PartId`. A whole-order refund becomes the amount charged less every part refund already recorded.
- **FR-006**: Every reader of parts treats a cancelled part as done:
  - the order summary does not wait for it;
  - a move of it is refused;
  - the customer's cancel-all check ignores it;
  - it is not earning;
  - insights leave it out.
- **FR-007**: Order, sale and staff responses carry `cancelledAt`, `cancelReason` and `cancelledBy` per part. The
  storefront shows them and offers the cancel with a reason.
- **FR-008**: Notice `PartCancelled` to the buyer, with data `orderId`, `reason` and optionally `shop`, declared in
  `notification-kinds.json`, with words in en and vi.

### Key Entities

- **Order part** (`order_shipments`): gains cancellation columns.
- **Refund** (Payment): gains `PartId`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Order tests cover:
  - cancelling one of two parts refunds exactly its lines and lets the other ship to `Shipped`;
  - a shipped part is 409, and another seller's part is 404;
  - a repeat is a no-op;
  - the last part cancels the order;
  - a cancelled part earns nothing;
  - the seller's shop voucher is given back;
  - a concurrent ship and cancel leave exactly one winner.
- **SC-002**: Inventory tests: held and confirmed reservations of the part come back and the rest stay put; a
  redelivery moves nothing.
- **SC-003**: Payment tests: a part refund once; a later whole-order refund is the remainder.
- **SC-004**: Storefront tests: the seller's cancel sends the reason; the buyer's order shows the cancelled part.
- **SC-005**: Bruno covers the seller's cancel, the no-op repeat, 404 for another's, and 401. `verify-saga.sh`
  passes.
- **SC-006**: Mutations make tests fail: the lock removed; the summary counting cancelled parts; Earning counting
  them; Payment refunding the full amount after a part refund; Inventory restocking the whole order.

## Decision

1. **Columns, not a status value** (research D1).
2. **The part's refund is its goods and tax; delivery stays unless it is the last part** (D2).
3. **Inventory decides from reservations**, filtered to the part's variants (D3).
4. **Payment owns "what is left"** (D4).
5. **Only the cancelled seller's voucher comes back** (D5).

## Assumptions

- A variant belongs to one seller, so an order's reservation per variant belongs wholly to one part (specs/035).

## Out of scope

- Cancelling some lines of a part; an email for the part cancellation (the buyer is notified in the app); a buyer
  asking a seller to cancel.
