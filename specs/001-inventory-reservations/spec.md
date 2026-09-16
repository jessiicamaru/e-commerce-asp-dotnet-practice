# Feature Specification: Inventory Reservations

**Feature Branch**: `001-inventory-reservations`

**Created**: 2026-09-16

**Status**: Draft

**Input**: User description: "Inventory microservice for the checkout saga. It owns stock levels and reservations in its own PostgreSQL database, consumes ReserveInventoryCommand and ReleaseInventoryCommand from RabbitMQ via MassTransit, and replies with InventoryReservedEvent or InventoryReservationFailedEvent so the existing OrderStateMachine saga can progress past the Submitted state. Consumers must be idempotent because the broker redelivers. Contracts already exist in Ecommerce.Contracts/Inventory."

## Context

Today a submitted order asks for stock and nobody answers. The checkout process stops at
"submitted" and stays there permanently — no confirmation, no rejection, no refund. Every order
ever placed is in that state. This feature supplies the missing answer, so an order either moves
forward with stock set aside for it, or is turned down promptly and explicitly.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper's order is backed by real stock (Priority: P1)

A shopper places an order for items that are in stock. The quantity they ordered is set aside for
them, so nobody else can buy the same units while their payment is being taken. The order moves on
to payment instead of stalling.

**Why this priority**: This is the only path that turns a placed order into a fulfillable one.
Without it the checkout process has no ending at all, which is the current state. Every other story
is a variation on this one.

**Independent Test**: Place an order for a product with ample stock and observe that the order
leaves the "submitted" state and that the available quantity for that product drops by the amount
ordered. Delivers the core value on its own: orders stop getting stuck.

**Acceptance Scenarios**:

1. **Given** a product with 10 units available, **When** an order for 3 units is placed, **Then**
   the order advances past "submitted" and the product shows 7 units available to other shoppers.
2. **Given** an order containing several different products, all sufficiently stocked, **When** the
   order is placed, **Then** every line is set aside together and the order advances once.
3. **Given** stock has been set aside for an order, **When** the order later completes
   successfully, **Then** the set-aside quantity is permanently deducted and not returned to
   available stock.

---

### User Story 2 - A shopper is told promptly that an item is unavailable (Priority: P2)

A shopper orders more than is available. Rather than waiting indefinitely, the order is rejected
and the shopper can be told why.

**Why this priority**: Without it, any order that cannot be met fails silently and sits in limbo —
the same defect as today, just narrower. It is second because it only matters once the happy path
exists.

**Independent Test**: Place an order for more units than exist and observe that the order reaches a
failed state carrying a reason, within seconds, and that available stock is unchanged.

**Acceptance Scenarios**:

1. **Given** a product with 2 units available, **When** an order for 5 units is placed, **Then**
   the order is rejected with a reason naming the unavailable item, and stock stays at 2.
2. **Given** an order containing one available and one unavailable item, **When** the order is
   placed, **Then** the whole order is rejected and **neither** item is set aside — an order is
   all-or-nothing.
3. **Given** an order references a product that is not known at all, **When** the order is placed,
   **Then** the order is rejected with a reason rather than being accepted or left pending.

---

### User Story 3 - Stock returns to the shelf when an order falls through (Priority: P3)

An order had stock set aside, but payment later failed. The set-aside quantity becomes available to
other shoppers again instead of being lost.

**Why this priority**: Without it, every failed payment permanently removes sellable stock. Damaging
over time, but only reachable once reservations exist, so it comes third.

**Independent Test**: Reserve stock for an order, trigger a payment failure, and observe available
stock return to its original level.

**Acceptance Scenarios**:

1. **Given** 3 units were set aside for an order, **When** that order's payment fails, **Then**
   available stock rises by 3 and the reservation is marked released.
2. **Given** a release arrives for an order that was never reserved, **When** it is processed,
   **Then** nothing changes and no error is surfaced to the shopper.
3. **Given** a reservation has already been released, **When** a second release for the same order
   arrives, **Then** stock is **not** credited twice.

---

### User Story 4 - Stock is not lost when a checkout dies mid-flight (Priority: P4)

An order had stock set aside, but the process handling it stopped before it could either complete
or fail — a crash, a lost message, a service restart. The held stock returns to the shelf on its
own instead of being unsellable forever.

**Why this priority**: It is a recovery path, not a normal one, so it only earns attention once the
three ordinary outcomes work. But without it every crash permanently shrinks sellable stock, and
the only remedy is editing the database by hand.

**Independent Test**: Reserve stock for an order, send neither confirmation nor release, and observe
that available stock returns to its original level once the holding period has elapsed.

**Acceptance Scenarios**:

1. **Given** 3 units were set aside and the holding period has elapsed with no confirmation or
   release, **When** the system next checks, **Then** the 3 units become available again and the
   reservation is marked expired.
2. **Given** a reservation has expired, **When** a late confirmation or release for that same order
   arrives, **Then** stock is left untouched and no error is raised.
3. **Given** a reservation is within its holding period, **When** the system checks, **Then** the
   reservation is left alone.

---

### Edge Cases

- **Repeated delivery of the same request.** The messaging system can deliver the same reservation
  or release request more than once. Processing it twice must not deduct or credit stock twice.
- **Two orders racing for the last unit.** When two orders arrive simultaneously for the final unit,
  exactly one succeeds and the other is rejected. Never both.
- **Partial availability within one order.** Some lines available, others not — the order is
  rejected whole; no line is left half-reserved.
- **Duplicate line items.** The same product listed twice in one order must be treated as the sum of
  both quantities, not evaluated line by line against full stock.
- **Zero or negative quantity.** An order line asking for zero or fewer units is rejected as invalid
  rather than silently succeeding.
- **Release arriving before reserve.** Out-of-order delivery must leave stock correct once both
  have been processed.
- **Unknown product.** A product the inventory has never heard of is treated as unavailable, not as
  infinitely available.
- **A product the catalogue knows but inventory has not been stocked with.** Registered with zero
  units, so orders for it are rejected for insufficient stock rather than as an unknown product.
- **Late arrival after expiry.** A confirmation or release for a reservation that already expired
  must leave stock untouched, rather than crediting units a second time.
- **Expiry racing a confirmation.** If the holding period elapses at the moment a confirmation is
  being processed, exactly one of the two outcomes takes effect, never both.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST track, for every product it knows about, the quantity on hand and the
  quantity currently set aside for orders, and derive from these the quantity available to new
  orders.
- **FR-002**: The system MUST respond to every stock request for an order with either a
  confirmation or a rejection. No request may go unanswered.
- **FR-003**: A stock request MUST be satisfied in full or not at all. Partial reservations are
  never created.
- **FR-004**: A rejection MUST carry a human-readable reason identifying why it could not be met.
- **FR-005**: The system MUST release previously set-aside stock on request, returning it to
  available.
- **FR-006**: Processing the same request more than once MUST have the same effect as processing it
  once. Repeated delivery must never double-deduct or double-credit stock.
- **FR-007**: Concurrent requests for the same product MUST NOT allow the combined reserved
  quantity to exceed the quantity on hand, under any interleaving.
- **FR-008**: The system MUST treat a request for an unknown product as unfulfillable.
- **FR-009**: The system MUST reject a request containing a non-positive quantity.
- **FR-010**: The system MUST record each reservation and its state so that an operator can
  determine which order is holding which stock, and when it was set aside or released.
- **FR-011**: The system MUST own its stock data independently of any other part of the platform,
  so that its availability decisions cannot be contradicted by another component.
- **FR-012**: The system MUST expose a way to inspect current stock for a product, so that stock
  levels can be verified without direct database access.
- **FR-013**: The system MUST be the sole authority on sellable quantity. The stock figure recorded
  by the product catalogue is descriptive only and MUST NOT be consulted for any availability
  decision.
- **FR-014**: The system MUST learn that a product exists from the catalogue's product-created
  notification, registering it with zero units on hand.
- **FR-015**: The system MUST provide a way to set and adjust the quantity on hand for a product,
  restricted to staff.
- **FR-016**: Stock set aside for an order MUST be held until it is confirmed, released, or
  expires. A reservation that receives neither confirmation nor release within a configured holding
  period MUST expire on its own and return its units to available.
- **FR-017**: A confirmation or release arriving for a reservation that has already expired MUST
  NOT change stock levels a second time, and MUST NOT be reported as an error.

### Key Entities

- **Stock Item**: What the inventory knows about one product — how many units are physically on
  hand, and how many of those are currently promised to orders. Available quantity is the
  difference. One per product.
- **Reservation**: A claim on stock by one order. Records which order, which product, how many
  units, its current state (held, released, or confirmed), and when each transition happened. Many
  per order, one per product line.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of placed orders reach a definite outcome — accepted or rejected — rather than
  remaining in an indeterminate state. Today this figure is 0%.
- **SC-002**: A shopper whose order can be fulfilled sees it move past the stock check within 5
  seconds, for 99% of orders under normal load.
- **SC-003**: A shopper whose order cannot be fulfilled is told so within 5 seconds, with a reason
  naming the problem item.
- **SC-004**: Across 100 orders placed simultaneously for a product with 10 units, exactly 10 units
  are sold. No overselling under any concurrency.
- **SC-005**: Replaying any stock request produces no additional change to stock levels, verified
  by processing the same request twice and comparing quantities.
- **SC-006**: After a failed payment, the stock that had been set aside is available to other
  shoppers again within 5 seconds, with no permanent loss of sellable stock.
- **SC-007**: Stock held by an order that is abandoned mid-flight returns to available within one
  holding period without anyone intervening. No manual database edits are ever required to recover
  stranded stock.
- **SC-008**: At rest — with no orders in flight — the units on hand equal the units available for
  every product, verifiable at any time.

## Assumptions

- The checkout process already places orders and already asks for stock; this feature supplies the
  missing answer rather than changing how orders are placed.
- The message contracts for requesting and releasing stock, and for confirming or rejecting a
  reservation, already exist and are not being redesigned.
- Stock is a simple count per product. Warehouses, locations, batches, serial numbers and
  variant-level stock are out of scope.
- Backorders, pre-orders and "notify me when back in stock" are out of scope.
- Restocking is handled by staff setting quantities directly. Purchase orders, supplier integration
  and goods-receipt workflows are out of scope.
- The holding period before an unconfirmed reservation expires is a configurable setting, defaulting
  to 15 minutes — comfortably longer than a payment attempt, short enough that stranded stock
  returns the same session.
- Products created before this feature exists start at zero units on hand and must be stocked
  explicitly. There is no migration of the catalogue's existing stock figures.
- The inventory's answer is trusted by the checkout process; there is no second confirmation step
  before payment is attempted.
- Orders are low enough in volume for this stage of the project that stock decisions can be made
  one order at a time rather than in batches.
