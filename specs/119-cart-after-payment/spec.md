# Feature Specification: The cart empties on screen when the order is paid

**Feature Branch**: `fix/242-cart-after-payment` | **Created**: 2026-10-01 | **Issue**: #242

**Status**: Draft

**Input**: Issue #242 - "the cart badge still counts what was just paid for" - found in the screen review of 2026-10-01.

## Why

Straight after paying, the order page said "Paid" while the header still said the cart held the camera just bought. A shopper reading that would think the purchase did not go through, or that they are about to buy it twice.

## User Scenarios & Testing *(mandatory)*

### US1 - The header's cart is right once the order settles (Priority: P1)



**Acceptance Scenarios**:

1. **Given** an order placed from the cart, **When** the order page sees it settle to Paid, **Then** the header's cart badge no longer counts the lines that were ordered - within a few seconds, without a reload.
2. **Given** the order fails instead, **Then** the cart is read again too and still holds the lines (Cart keeps them on a failure, specs/010).
3. **Given** an order page opened on an order that is already settled, **Then** nothing extra is fetched.

### Edge Cases

- Cart removes the lines on `OrderCompletedEvent`, the same event Order settles on: the order page can see Paid a moment before Cart has applied it, so the cart is read on the settlement and once more shortly after.
- Anything added to the cart during checkout stays (Cart decrements, specs/010) - the client only re-reads.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When an order the page watches leaves the settling states, the cart is re-read at once and once more after `CART_SETTLE_RECHECK_MS` (2 s).
- **FR-002**: No re-read for an order already settled when the page opened.
- **FR-003**: The browser flows assert the header's cart badge is gone after the order is paid.

## Success Criteria *(mandatory)*

- **SC-001**: After paying, the cart badge disappears without a reload (checked in a browser).
- **SC-002**: A hook test fails when the settlement does not re-read the cart.

## Assumptions

- The server is right already: Cart removes the ordered lines on completion (specs/010). This is the client not asking again.
