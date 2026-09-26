# Feature Specification: Cancelling a paid order

> Completed on 2026-09-27, after the feature merged (#84), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature branch**: `039-order-cancellation`
**Created**: 2026-09-23
**Status**: Merged (#84, 2026-09-23)
**Issue**: none - the pull request closes no issue.

## What is wrong

Once the checkout saga completes, an order can only move forward - Paid → Preparing → Shipped.
`OrderStatus.Cancelled` has been in the enum since the beginning and nothing reaches it. A customer who
changes their mind an hour after paying has no way out, and neither has the shop when it cannot fulfil.

Since specs/037 this also matters for money: a paid order's parts are on their way to being owed to
sellers, and nothing could take that back.

## Decisions taken with the user (2026-09-23)

- **A customer can cancel while nobody has started on it** - every parcel still waiting. Once any
  parcel is being prepared only staff can cancel; once any parcel has been shipped, nobody can.
- **Only the whole order.** No cancelling one parcel of a multi-seller order.

## User Scenarios & Testing

### US1 - A customer cancels (Priority: P1)

**Why this priority**: the gap itself - a customer who changes their mind has no way out.

**Independent Test**: as a customer, cancel a paid order whose parcels are all waiting; it reads
cancelled, and a second cancel changes nothing.

**Acceptance Scenarios**:

1. **Given** a paid order whose parcels are all still waiting, **When** the customer cancels after
   confirming, **Then** it is cancelled.
2. **Given** it was cancelled, **When** the customer reads it or their list, **Then** the order reads as
   cancelled.
3. **Given** any parcel is being prepared or has been shipped, **When** the customer tries to cancel,
   **Then** they are told why they cannot.
4. **Given** it is already cancelled, **When** they cancel again, **Then** nothing changes and it is not an
   error; **Given** someone else's order, **When** they try, **Then** it is "not found".

---

### US2 - What a cancellation undoes (Priority: P1)

**Why this priority**: a cancellation that leaves the stock taken, the charge kept and the seller owed is
worse than none.

**Independent Test**: record stock before an order, place and pay it, cancel it, and see the stock back
where it was and one refund of the full charge.

**Acceptance Scenarios**:

1. **Given** a cancelled order, **When** its effects settle, **Then** the goods go back on the shelf: the
   stock is exactly what it was before the order.
2. **Given** a cancelled order, **When** Payment hears of it, **Then** a refund of what was charged is
   recorded against the payment - Payment is a stub, so, like a payout, it is a ledger entry and no money
   moves.
3. **Given** a cancelled order, **When** balances and payouts are computed, **Then** no seller is ever owed
   anything for it, and no payout can claim its parts.
4. **Given** the cancellation is delivered more than once, **When** each service handles it, **Then** each
   effect happens once.

---

### US3 - Staff cancel (Priority: P2)

**Why this priority**: the shop that cannot fulfil needs a way out too, but the customer's path is the
common one.

**Independent Test**: as an administrator, cancel an order whose parcel is being prepared; then try one
with a shipped parcel and be refused.

**Acceptance Scenarios**:

1. **Given** a paid order with no parcel shipped, **When** staff cancel it from the console, **Then** it is
   cancelled, until its first parcel has been shipped.
2. **Given** a cancelled order holding a seller's goods, **When** the seller reads the sale, **Then** they
   see it as cancelled, with no address and nothing to ship, and cannot move it.

---

### Edge cases

- **Cancel and ship at the same moment**: exactly one wins. Either the order is cancelled and nothing
  ships, or a parcel shipped and the cancellation is refused.
- **The cancellation reaches Inventory before the order's completion did** (two message types, no order
  between them - #15): the held units are released instead of returned, and the late completion
  finds nothing to deduct.
- **An order still settling, or failed**: not cancellable - there is nothing to undo yet, or already
  nothing.
- **An older order with no parts, in `Preparing`**: its parts are created in the order's state first, so
  it is seen as being prepared, not as waiting.
- **A seller with no part on a cancelled order** trying to move it: the usual 404, never "cancelled".
- **A payment that was rejected, or none at all**: no refund is recorded.

## Requirements

### Functional Requirements

- **FR-001** Cancelling MUST be refused once any parcel has shipped, for everyone.
- **FR-002** A customer MUST be refused once any parcel is being prepared; staff are not.
- **FR-003** Cancelling and shipping MUST be serialised per order.
- **FR-004** A cancelled order MUST restore stock, record one refund, and owe sellers nothing - each
  effect exactly once.
- **FR-005** The order MUST record who cancelled it (the customer or staff).
- **FR-006** A customer can cancel only their own order; not-theirs looks like not-there.

### Key Entities

- **Order**: gains who cancelled it - the customer or staff. `Cancelled` was always one of its states.
- **Refund**: what was given back for a cancelled order - the whole of the payment, in its currency,
  once per order, through the same (stub) provider.
- **Reservation**: unchanged in shape; a returned one ends released, with the reason saying it was
  returned.

## Out of scope

- Partial cancellation, returns after delivery, a reason typed by the customer, cancellation fees.
- Moving money: Payment is still a stub.

## Success Criteria

- **SC-001** After a cancellation, on-hand and reserved stock equal their values before the order.
- **SC-002** A cancelled order appears in no seller balance and no payout.
- **SC-003** `verify-saga.sh` passes, including a new cancellation scenario.

## Assumptions

*(Added in this backfill.)*

- "Staff" is `Admin`; the staff cancel route is `[Authorize(Roles = "Admin")]`.
- Inventory's reservation rows and Payment's payment row are each service's own truth about what an order
  took, so the event carries neither items nor an amount (research D1).
