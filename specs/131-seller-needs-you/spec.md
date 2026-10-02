# Feature Specification: A seller's home says what needs them

**Feature Branch**: `feat/247-seller-needs-you` | **Created**: 2026-10-02 | **Issue**: #247

**Status**: Draft

**Input**: Issue #247 - "a seller's home says what needs them" - found in the screen review of 2026-10-01.

## Why

A new sale waiting to be prepared was a line of small grey text on `/shop`; a question waiting for an answer and a return waiting for a decision were visible only by opening their pages; a missing payout account was said only on Payouts, where a seller goes once they expect money. And the overview's "Your takings" showed the goods before commission, which a new seller reads as what they will be paid.

## User Scenarios & Testing *(mandatory)*

### US1 - The seller sees what is waiting for them, and goes straight to it (Priority: P1)



**Acceptance Scenarios**:

1. **Given** a sale whose part is waiting to be prepared, a question not answered, a return requested and no payout account, **Then** `/shop` opens with a "Needs you" panel listing each with its count and a link to the place it is done.
2. **Given** nothing waiting, **Then** the panel says all is done rather than disappearing silently.
3. **Given** the seller's menu, **Then** Sales, Questions and Returns carry the same counts as badges, on every `/shop` page.

---

### US2 - The numbers say what they are (Priority: P2)



**Acceptance Scenarios**:

1. **Given** the overview, **Then** the revenue reads "Sales before commission", not "Your takings".
2. **Given** the sales list, **Then** its subtitle says the customer's address is shown while their part is waiting or being prepared - not that they never see it.

### Edge Cases

- "To prepare" is the seller's **part** waiting (`Paid`), not cancelled, on an order that is not cancelled - the same state the sale page's Start preparing acts on.
- Counts are read with the layout and every minute and on focus, like the staff console's (specs/129), under keys of their own (specs/130).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `GET /api/orders/sales` takes an optional `status` - `Paid`, `Preparing`, `Shipped`, `Cancelled` - of the caller's own part; `Paid` excludes cancelled parts and orders. 400 for any other value.
- **FR-002**: `useSellerWaiting()` reads: sales to prepare (`status=Paid`), unanswered questions, returns requested, and whether a payout account exists.
- **FR-003**: `/shop` shows a Needs you panel from it; the seller menu badges Sales, Questions, Returns.
- **FR-004**: Labels corrected: "Sales before commission"; the sales subtitle's address sentence.

## Success Criteria *(mandatory)*

- **SC-001**: A seller with work waiting sees each count on the home and the menu (tested); the sales filter counts across all pages, not a window (tested against PostgreSQL).

## Assumptions

- The window of 100 sales the overview reads stays for its revenue figure; the counts no longer depend on it.
