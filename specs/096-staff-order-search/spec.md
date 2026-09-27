# Feature Specification: Staff find any order

**Feature Branch**: `096-staff-order-search` | **Created**: 2026-09-27 | **Issue**: #194

**Status**: Merged (#203, 2026-09-27)

**Input**: Issue #194 - "staff cannot find an order outside the fulfilment queue".

## Why

A customer writes: "my order 01a0dd2b failed - where is my money?". The administrator's console lists orders only as a
fulfilment queue - Paid, Preparing, Shipped, and only by the shop's own parcel (specs/035, 038). A failed, cancelled or
still-submitted order cannot be listed at all, and nothing finds an order by its id or by the customer who placed it.
`GET /api/orders/fulfilment/{id}` reads any order, but only for somebody who already knows its full id.

## User Scenarios & Testing *(mandatory)*

### US1 - Find an order by the start of its id (Priority: P1)

An administrator types the first characters a customer quoted (`01a0dd2b`) and sees the matching orders, whatever their
status, newest first, each opening the staff view of the order.

**Why this priority**: The customer-service case in the issue; the order id is what every email and page shows.

**Independent Test**: Place three orders; search by the first 8 characters of one's id: exactly that one is listed,
with its status, total and currency.

**Acceptance Scenarios**:

1. **Given** orders in several statuses, **When** an administrator searches by an id prefix, **Then** the matching orders
   are listed newest first, with their status (Failed and Cancelled included).
2. **Given** a prefix that is not hexadecimal, **Then** 400.

---

### US2 - Find a customer's orders (Priority: P1)

An administrator types an email; the storefront finds the person (Identity's staff search, specs/043) and lists their
orders - every status, newest first.

**Why this priority**: The other half of customer service: "I placed an order yesterday".

**Independent Test**: A customer with two orders and another with one: filtering by the first customer's id lists exactly
their two.

**Acceptance Scenarios**:

1. **Given** a customer id, **When** an administrator asks for their orders, **Then** exactly that customer's orders.
2. **Given** an email with no account, **Then** the page says nobody has that address; nothing is asked of Order.

---

### US3 - Every status, filterable (Priority: P2)

The list can be narrowed to one status - Submitted, Paid, Preparing, Shipped, Failed, Cancelled - or show all.

**Why this priority**: "Show me today's failed orders" is the other question staff ask.

**Independent Test**: With a failed and a paid order, filtering by Failed lists the failed one only.

**Acceptance Scenarios**:

1. **Given** a status, **Then** only orders in it (Paid includes the legacy `Completed`, as every read does).
2. **Given** an unknown status, **Then** 400 naming the allowed ones.

---

### US4 - Only administrators (Priority: P1)

The route reads every customer's orders, like `GET /api/orders/fulfilment/{id}`, so it is administrators only.

**Why this priority**: It is an owner-unscoped read (specs/038's warning).

**Acceptance Scenarios**:

1. **Given** a customer, seller or moderator token, **Then** 403.
2. **Given** no token, **Then** 401.

### Edge Cases

- **A prefix shorter than 4 characters.** 400: two characters match a sixteenth of the table, which is not a search.
- **Hyphens.** Accepted where a Guid has them (`01a0dd2b-5f...`); case-insensitive.
- **Both an id prefix and a customer.** Both apply.
- **The shop's parcels vs a seller's.** The list is of orders, not parcels; the staff view shows every parcel.
- **Customer names on the list.** Drawn by the storefront through `/api/users/lookup` (Admin, specs/047) - Order does not
  learn emails or names.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `GET /api/orders/staff?status=&search=&customerId=&page=&pageSize=` (Admin) lists orders newest first, with
  the customer's id on each row.
- **FR-002**: `search` is an order-id prefix of 4-36 hexadecimal characters or hyphens, matched case-insensitively.
- **FR-003**: `status` is optional; when given it is one of Submitted, Paid, Preparing, Shipped, Failed, Cancelled, and
  Paid includes Completed.
- **FR-004**: `pageSize` 1-50, `page` ≥ 1.
- **FR-005**: `/admin/orders` gains a "Find an order" view: one box that takes an id prefix or an email, a status filter,
  the list with each customer's name and email, and a link to the existing staff order view.
- **FR-006**: No other route, table or message changes.

### Key Entities

- **Order** (`orders`) - id, user id, status, totals, currency, created (unchanged).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Order tests: by id prefix, by customer, by status (Paid including Completed), newest first, and the
  validator's refusals.
- **SC-002**: Bruno: an administrator finds the collection's order by its prefix; a customer's token is 403.
- **SC-003**: Storefront tests: an id prefix goes to Order; an email goes to Identity first and then Order by the id found;
  an unknown email asks Order nothing.
- **SC-004**: Mutations - the status filter ignored, the prefix match ignored, the route opened to customers - each red.

## Decision

1. **Search by email is composed by the storefront, not by Order.** Order knows user ids, never emails (Principle I); the
   administrator overview already composes Order with Identity's lookup (specs/047). Rejected: Order calling Identity to
   resolve an email - a new synchronous edge for a staff screen.
2. **Administrators only, not moderators.** The same permission as every other owner-unscoped order read (specs/038).
   Recorded as decided on the user's behalf ([research.md](research.md) D1, D2).

## Assumptions

- Staff know either an id prefix or the customer's email; searching by product or amount is not asked for.

## Out of scope

- Moderator access to orders.
- Exporting orders.
