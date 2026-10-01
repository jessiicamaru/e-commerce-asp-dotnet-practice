# Feature Specification: Order lines fit any width

**Feature Branch**: `fix/239-order-lines-narrow` | **Created**: 2026-10-01 | **Issue**: #239

**Status**: Draft

**Input**: Issue #239 - "order lines clip their totals in narrow containers" - found in the screen review of 2026-10-01.

## Why

The lines of an order are where a shopper checks what they are paying for. At checkout the summary column is 24rem wide and the line total was cut to "₫1,250,00"; on a phone the order page did the same and wrapped the product into six lines. A number with its last digit missing is a wrong number, on the page whose point is money. The rule above the total was also broken in two.

## User Scenarios & Testing *(mandatory)*

### US1 - Every line's total can be read, at checkout, on an order and on a phone (Priority: P1)

A clipped amount is a wrong amount.

**Acceptance Scenarios**:

1. **Given** a line with a long product name and SKU in the checkout summary, **Then** its total is shown whole, inside the card, and the name wraps beside it.
2. **Given** the order page at 390px, **Then** each line shows its name and details, then "quantity × price" under them, and the total at the right - nothing past the edge.
3. **Given** a line a voucher discounted, **Then** the discount stays under that line's total.

---

### US2 - The total reads as one line (Priority: P2)



**Acceptance Scenarios**:

1. **Given** any order's totals, **Then** the rule above "Total" runs under both the label and the amount.
2. **Given** a wide order page, **Then** the totals sit at the right, under the line totals.

### Edge Cases

- A SKU with no spaces (`E2ECAMERA1790837359318`) breaks anywhere rather than widening the line.
- The seller's sale page and the staff order page use the same lines and get the same layout.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Order lines are a two-column list - details (shrinkable) and the total (never wraps) - with the quantity and unit price under the details, at every width.
- **FR-002**: Long words in the details break rather than widen the line.
- **FR-003**: The totals' last row is one row with one rule across it; the totals block sits at the right of a wide page.
- **FR-004**: A browser check fails if a line total at checkout reaches past its card.

## Success Criteria *(mandatory)*

- **SC-001**: At checkout and at 390px no line total is clipped (checked in a browser).
- **SC-002**: The browser check fails with the old table layout.

## Assumptions

- The list is used by checkout, the order page, a seller's sale and the staff order page; all four change together.
