# Feature Specification: Each seller ships their own part

> Completed on 2026-09-27, after the feature merged (#79), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature branch**: `035-seller-shipments`
**Created**: 2026-09-23
**Status**: Merged (#79, 2026-09-23)
**Closes**: #76

## What is wrong

Since specs/034 a seller can see that something they listed sold. They cannot do anything about it.
Preparing and shipping are administrator-only, and they act on the **order** - one status, one
tracking reference - while an order can hold goods from several sellers and the shop. Two sellers on
one order are two parcels with two tracking numbers, and an order that can be half sent. A single
status cannot say that, and a shop whose administrator packs every seller's parcel is not a
marketplace.

## User Scenarios & Testing

### US1 - A seller prepares and ships their part (Priority: P1)

**Why this priority**: it is issue #76 - a seller who can see a sale and cannot send it does not have a
shop.

**Independent Test**: on a paid order holding two sellers' goods, each seller prepares and ships their
own part through the API; the other's attempt on it is "not found".

**Acceptance Scenarios**:

1. **Given** a paid order holding a seller's goods, **When** the seller moves their part to "being
   prepared" and then to "shipped" with a tracking reference, **Then** each step is accepted.
2. **Given** a step already taken, **When** it is asked for again, **Then** it is harmless; **Given** a
   part in any other state, **When** a step is skipped or taken backwards, **Then** it is refused.
3. **Given** another seller's part, or the shop's, **When** a seller tries to move it, **Then** the
   refusal is the same "not found" as for an order that does not exist.
4. **Given** an order not yet paid, or failed, **When** a seller tries to prepare their part, **Then** it
   cannot be prepared.

---

### US2 - The seller sees where to send it, and only while they need to (Priority: P1)

**Why this priority**: a seller cannot ship without an address, and specs/034 withheld it until
shipping became their job - which it now is.

**Independent Test**: read the sale before and after shipping; the address is there, then gone.

**Acceptance Scenarios**:

1. **Given** the seller's part is waiting or being prepared, **When** they read the sale, **Then** they
   see the delivery address and phone.
2. **Given** their part is shipped, **When** they read the sale, **Then** the address is no longer shown.
3. **Given** any sale, **When** it is read, **Then** the customer's account (id, email) is never shown.

---

### US3 - The customer can tell a half-sent order from a sent one (Priority: P1)

**Why this priority**: an order that is half sent and reads "shipped" is a support ticket; one that
reads "preparing" for ever is another.

**Independent Test**: ship one of two parts; the customer's order shows two parcels, one shipped, and
the list says "1 of 2".

**Acceptance Scenarios**:

1. **Given** an order with several parts, **When** the customer opens it, **Then** it shows each part:
   what is in it, whether it is waiting, being prepared or shipped, and its tracking reference.
2. **Given** an order partly sent, **When** the customer reads their order list, **Then** it says "1 of 2
   parcels shipped".
3. **Given** an order sent in one parcel, **When** it is read, **Then** it reads exactly as it did before.

---

### US4 - The shop ships its own part the same way (Priority: P1)

**Why this priority**: one model for everybody who ships means the administrator's existing path must
become "the shop's part", or there are two models to keep in step.

**Independent Test**: on an order of shop goods and a seller's goods, the administrator's prepare and
ship move only the shop's parcel; on an order with no shop goods they are refused with a reason.

**Acceptance Scenarios**:

1. **Given** an order holding the shop's own goods, **When** an administrator prepares or ships it,
   **Then** the action moves the **shop's own part** of the order.
2. **Given** an order with no shop goods, **When** an administrator tries to ship it, **Then** there is
   nothing for them to ship, and the answer says so.
3. **Given** the administrator's queue, **When** it is filtered by state, **Then** it lists orders by
   where the shop's part is.

---

### US5 - The storefront (Priority: P2)

**Why this priority**: the API alone completes US1-US4; people need the pages.

**Independent Test**: as a seller, ship a part from the sale page; as the customer, see the parcels.

**Acceptance Scenarios**:

1. **Given** a seller's sale page, **When** it loads, **Then** it offers the next step, asks for a
   tracking reference in a dialog, and shows the address while it is needed.
2. **Given** a customer's order page, **When** it loads, **Then** it lists the parcels.
3. **Given** either page, **When** it is read in Vietnamese or English, **Then** both languages are
   there, and the pages have unit tests.

---

### Edge cases

- **Two sellers ship at the same moment.** The order must still end "shipped" once both are.
- **Orders placed before this feature.** They become one part per seller (or the shop), in the state
  the order was in.
- **An order written by an older version** of the service during a rollback has no parts. The first
  fulfilment step on it creates them, in the state the order is in.
- **The same ship step with a different tracking reference.** Not a repeat: refused with 409, naming
  the reference already recorded.
- **An administrator on an order that is not paid.** 409 naming the order's state; for a seller the
  same situation is the one 404, because a seller must not learn the order exists.

## Requirements

### Functional Requirements

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

### Key Entities

- **Shipment part**: one seller's share of one order - one parcel. Whose it is (a seller, or nobody for
  the shop's own goods), where it has got to (waiting, being prepared, shipped), its tracking reference
  once shipped, and when it last moved. Exactly one per seller per order, and at most one for the shop.
- **Order**: unchanged in shape; its status and tracking reference become a **summary** of its parts.

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

## Assumptions

*(Added in this backfill; the original spec had none stated.)*

- The seller of an order line is the one frozen on it at checkout (`order_items.SellerId`, specs/034).
  A part belongs to that seller whoever owns the product now.
- Orders from before specs/034 recorded no seller, so all their lines form the shop's part.
- The Order database runs PostgreSQL 15 or later (`NULLS NOT DISTINCT`); the containers run 16.
