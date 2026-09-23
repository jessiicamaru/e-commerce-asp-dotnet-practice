# Feature Specification: Each seller ships their own part

**Feature branch**: `035-seller-shipments`
**Created**: 2026-09-23
**Status**: Draft
**Closes**: #76

## What is wrong

Since specs/034 a seller can see that something they listed sold. They cannot do anything about it.
Preparing and shipping are administrator-only, and they act on the **order** - one status, one
tracking reference - while an order can hold goods from several sellers and the shop. Two sellers on
one order are two parcels with two tracking numbers, and an order that can be half sent. A single
status cannot say that, and a shop whose administrator packs every seller's parcel is not a
marketplace.

## User Scenarios

### US1 - A seller prepares and ships their part (P1)

**Acceptance**
1. A seller moves their part of a paid order to "being prepared", then to "shipped" with a tracking
   reference.
2. Doing the same step again is harmless; skipping a step, or going backwards, is refused.
3. A seller cannot move another seller's part, or the shop's - the refusal is the same "not found"
   as for an order that does not exist.
4. A part cannot be prepared before the order is paid, and never on a failed order.

### US2 - The seller sees where to send it, and only while they need to (P1)

**Acceptance**
1. While their part is waiting or being prepared, the seller sees the delivery address and phone.
2. Once their part is shipped, the address is no longer shown to them.
3. The customer's account (id, email) is never shown.

### US3 - The customer can tell a half-sent order from a sent one (P1)

**Acceptance**
1. An order shows each part: what is in it, whether it is waiting, being prepared or shipped, and its
   tracking reference.
2. The order list says "1 of 2 parcels shipped" for an order that is partly sent.
3. An order sent in one parcel reads exactly as it did before.

### US4 - The shop ships its own part the same way (P1)

**Acceptance**
1. An administrator's prepare and ship act on the **shop's own part** of the order.
2. An order with no shop goods has nothing for the administrator to ship, and says so.
3. The administrator's queue lists orders by where the shop's part is.

### US5 - The storefront (P2)

**Acceptance**
1. A seller's sale page offers the next step, asks for a tracking reference in a dialog, shows the
   address while it is needed.
2. A customer's order page lists the parcels.
3. Both languages; unit tests.

### Edge cases

- **Two sellers ship at the same moment.** The order must still end "shipped" once both are.
- **Orders placed before this feature.** They become one part per seller (or the shop), in the state
  the order was in.
- **An order written by an older version** of the service during a rollback has no parts. The first
  fulfilment step on it creates them, in the state the order is in.

## Requirements

- **FR-001** Every order MUST have one part per seller whose goods it holds, plus one for the shop's
  own goods if it holds any.
- **FR-002** A part moves waiting → preparing → shipped, one step at a time, each step a guarded
  single statement; repeating a step changes nothing; any other move is refused.
- **FR-003** Only the part's seller may move a seller's part; only an administrator may move the
  shop's. Anyone else is refused as "not found".
- **FR-004** A part may only be prepared once its order is paid.
- **FR-005** Shipping a part MUST record a tracking reference.
- **FR-006** The order's own status MUST stay one the previous version understands: paid until any
  part starts, preparing while some part has started and not every part is shipped, shipped when
  every part is.
- **FR-007** The order's own tracking reference MUST be the part's when there is exactly one part,
  and empty otherwise.
- **FR-008** A seller MUST see the delivery address only while their part is waiting or being
  prepared.
- **FR-009** The customer MUST see every part's state and tracking reference.
- **FR-010** Two parts moved concurrently MUST leave the order's status consistent with both.
- **FR-011** Orders that exist today MUST get their parts without losing their state.

## Out of scope

- **Splitting the delivery charge** between parts. The customer paid one charge for one delivery
  choice; who owes what to whom is payouts, which nothing here does.
- **Showing the customer which shop each parcel comes from.** Order does not hold shop names; the
  parcel lists its goods, which is what a customer recognises.
- **Cancelling or returning a part.**

## Success Criteria

- **SC-001** On an order with two sellers, each moves only their own part; the other's attempt is
  "not found".
- **SC-002** The order reads "shipped" exactly when every part is, including when two are shipped at
  once.
- **SC-003** A seller sees the address before shipping and not after.
- **SC-004** An order in one parcel behaves, for the customer and for the administrator, as it did
  before this feature - `verify-saga.sh` passes unchanged.
