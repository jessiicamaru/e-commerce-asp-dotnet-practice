# Feature Specification: The seller sidebar keeps to its column

**Feature Branch**: `fix/238-seller-sidebar-overflow` | **Created**: 2026-10-01 | **Issue**: #238

**Status**: Draft

**Input**: Issue #238 - "the seller sidebar covers the page when the shop name is long" - found in the screen review of 2026-10-01.

## Why

Every page a seller works from was partly hidden under the sidebar once the shop's name was long enough: the page title, the first column of a table, form fields, and on a sale the very button that starts preparing it. A shop name is the seller's own choice and nothing limits how long it reads, so the frame has to hold any name.

## User Scenarios & Testing *(mandatory)*

### US1 - Every seller page is fully visible whatever the shop is called (Priority: P1)

A seller with a long shop name could not see or press what the page was for.

**Acceptance Scenarios**:

1. **Given** a shop named with 40 or more characters, **When** the seller opens any `/shop` page on a desktop, **Then** the sidebar stays inside its column, the name is cut short with an ellipsis, and nothing of the page is under it.
2. **Given** the same shop, **When** the seller opens a sale waiting to be prepared, **Then** "Start preparing" can be seen and pressed.
3. **Given** a phone, **Then** the shop card is no wider than the screen.

### Edge Cases

- A name with no spaces (one long word) truncates the same way.
- The full name is still readable: it is the card's title attribute, and the rename dialog shows it whole.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The seller sidebar never grows wider than its column, whatever the shop name.
- **FR-002**: A name too long for the card is truncated with an ellipsis and offered whole as a tooltip.
- **FR-003**: A browser check signs in as a seller with a long shop name and fails if the sidebar reaches into the page.

## Success Criteria *(mandatory)*

- **SC-001**: With a 60-character shop name, the sidebar's right edge is left of the page content on every seller page (checked in a browser).
- **SC-002**: The check fails with the fix removed.

## Assumptions

- Layout cannot be measured in jsdom; the evidence is the Playwright suite against a real browser.
