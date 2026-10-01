# Feature Specification: One account menu everywhere

**Feature Branch**: `feat/254-account-menu` | **Created**: 2026-10-01 | **Issue**: #254

**Status**: Draft

**Input**: Issue #254 - "one account menu everywhere" - found in the screen review of 2026-10-01.

## Why

Where a signed-in person could go depended on where they looked: the desktop menu had Saved and Open a shop but not Notifications or two-factor sign-in; the phone menu had neither Saved nor Notifications nor Open a shop; the account page offered four underlined words. Three lists, each written by hand, each missing something - and the account pages sat at three different widths, their headings jumping from x=160 to 384 to 432.

## User Scenarios & Testing *(mandatory)*

### US1 - The same places from every menu (Priority: P2)



**Acceptance Scenarios**:

1. **Given** a signed-in customer, **Then** the desktop menu, the phone menu and the account page offer the same destinations in the same order: account, orders, saved, notifications, addresses, two-factor sign-in, and Open a shop (or My shop for a seller), and the console for staff.
2. **Given** a seller, **Then** every one of them offers My shop instead of Open a shop.
3. **Given** the account pages, **Then** they start at the same left edge as the rest of the shop.

### Edge Cases

- The cart stays an icon in the header on every width, with its count - it is not repeated in the lists.
- Drawing only, as before: the pages behind each entry refuse whom they refuse on their own.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: One list of account destinations (`accountDestinations({ isSeller, isStaff })`) drives the desktop menu, the phone menu and the account page.
- **FR-002**: The account page shows it as a grid of labelled links with icons.
- **FR-003**: The account, two-factor and open-shop pages are left-aligned like the others (no `mx-auto`).

## Success Criteria *(mandatory)*

- **SC-001**: The three places list the same destinations for a customer, a seller and staff (tested).

## Assumptions

- Labels come from `common:nav.*`; two new keys (notifications, two-factor).
