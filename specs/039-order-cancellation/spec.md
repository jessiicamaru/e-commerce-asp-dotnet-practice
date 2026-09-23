# Feature Specification: Cancelling a paid order

**Feature branch**: `039-order-cancellation`
**Created**: 2026-09-23
**Status**: Draft

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

## User Scenarios

### US1 - A customer cancels (P1)

**Acceptance**
1. On a paid order whose parcels are all still waiting, the customer can cancel, after confirming.
2. The order then reads as cancelled, for them and in their list.
3. Once any parcel is being prepared or has been shipped, the customer is told why they cannot.
4. Cancelling again changes nothing and is not an error; someone else's order is "not found".

### US2 - What a cancellation undoes (P1)

**Acceptance**
1. The goods go back on the shelf: the stock is exactly what it was before the order.
2. A refund of what was charged is recorded against the payment - Payment is a stub, so, like a payout,
   it is a ledger entry and no money moves.
3. No seller is ever owed anything for a cancelled order, and no payout can claim its parts.
4. Each of these happens once, however many times the cancellation is delivered.

### US3 - Staff cancel (P2)

**Acceptance**
1. From the console, staff cancel a paid order until its first parcel has been shipped.
2. A seller sees their sale as cancelled, with no address and nothing to ship, and cannot move it.

### Edge cases

- **Cancel and ship at the same moment**: exactly one wins. Either the order is cancelled and nothing
  ships, or a parcel shipped and the cancellation is refused.
- **The cancellation reaches Inventory before the order's completion did** (two message types, no order
  between them - #15): the held units are released instead of returned, and the late completion
  finds nothing to deduct.
- **An order still settling, or failed**: not cancellable - there is nothing to undo yet, or already
  nothing.

## Requirements

- **FR-001** Cancelling MUST be refused once any parcel has shipped, for everyone.
- **FR-002** A customer MUST be refused once any parcel is being prepared; staff are not.
- **FR-003** Cancelling and shipping MUST be serialised per order.
- **FR-004** A cancelled order MUST restore stock, record one refund, and owe sellers nothing - each
  effect exactly once.
- **FR-005** The order MUST record who cancelled it (the customer or staff).
- **FR-006** A customer can cancel only their own order; not-theirs looks like not-there.

## Out of scope

- Partial cancellation, returns after delivery, a reason typed by the customer, cancellation fees.
- Moving money: Payment is still a stub.

## Success Criteria

- **SC-001** After a cancellation, on-hand and reserved stock equal their values before the order.
- **SC-002** A cancelled order appears in no seller balance and no payout.
- **SC-003** `verify-saga.sh` passes, including a new cancellation scenario.
