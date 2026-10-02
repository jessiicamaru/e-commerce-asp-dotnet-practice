# Feature Specification: Orders are recognisable

**Feature Branch**: `feat/248-recognisable-orders` | **Created**: 2026-10-02 | **Issue**: #248

**Status**: Draft

**Input**: Issue #248 - "orders are recognisable - a short reference, products and a status" - found in the screen review of 2026-10-01.

## Why

A customer's orders, a seller's sales and the staff queues each named an order by a timestamp to the second and "1 item · total" - two orders of the same day could not be told apart without opening both. The order page showed a 36-character id while the notice about the same order called it "01a0f63a", and the staff order page showed no status at all.

## User Scenarios & Testing *(mandatory)*

### US1 - An order is named the same way everywhere (Priority: P1)



**Acceptance Scenarios**:

1. **Given** any list of orders or the order page, **Then** the order reads by its short reference - the first eight characters of its id, as the notices write it - and the order page offers to copy it.
2. **Given** a notice naming "01a0f63a", **Then** the same eight characters are on the order's row and page.

---

### US2 - A row says what was bought and where it stands (Priority: P1)



**Acceptance Scenarios**:

1. **Given** a customer's orders, a seller's sales or a staff list, **Then** each row shows the first product's picture, up to three product names (and "+ n more"), a coloured status chip, and the whole row is the link.
2. **Given** a seller's sale on an order mixing sellers, **Then** only the seller's own products are named.

---

### US3 - Staff see an order's status (Priority: P2)



**Acceptance Scenarios**:

1. **Given** the staff order page, **Then** the order's status is a chip beside its title.

### Edge Cases

- Names are as frozen at checkout, in the order's language - the record of a purchase (specs/021).
- A product since deleted or taken off the shelf has no picture to show: the lens tile stands in, as everywhere.
- The short reference is for reading; addresses and links keep the full id.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The customer list, the fulfilment queue, the staff search and the seller's sales each carry `lines`: up to three of (productId, variantId, productName), the biggest first - for sales, the caller's own lines only.
- **FR-002**: One client helper words the short reference; one `OrderStatusChip` colours a status; one `OrderRow` draws a row (picture, reference, names, date, status, amount) as a single link, used by the customer's orders, the seller's sales and both staff lists.
- **FR-003**: The order page and the staff order page show the reference with a copy button; the staff page shows the status.

## Success Criteria *(mandatory)*

- **SC-001**: Every order list shows names and a status chip, and the same short reference as the notices (tested).

## Assumptions

- Eight characters are unique enough to read by among one person's orders; the full id stays in every link.
