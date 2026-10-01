# Feature Specification: The catalogue on a phone

**Feature Branch**: `fix/245-catalogue-phone` | **Created**: 2026-10-01 | **Issue**: #245

**Status**: Draft

**Input**: Issue #245 - "the catalogue on a phone is one long column" - found in the screen review of 2026-10-01.

## Why

Most shoppers arrive on a phone. There the catalogue was one product per row - 6,453px for twelve cameras - and the first product was ~1,350px down, under the hero, the categories and the filters. The price filter put "Max" alone on a second line, and a product whose photograph had not loaded yet was a blank white box.

## User Scenarios & Testing *(mandatory)*

### US1 - A shopper on a phone sees products straight away, two to a row (Priority: P1)



**Acceptance Scenarios**:

1. **Given** the catalogue at 390px, **Then** products are two to a row and the page does not scroll sideways.
2. **Given** the landing view at 390px, **Then** the first product starts within the first two screens: the hero is words and the button without the featured photo, the categories one scrolling row.
3. **Given** the price filter at 390px, **Then** Min – Max sit on one row.
4. **Given** a photograph still loading, **Then** its frame is tinted, not blank.

### Edge Cases

- Desktop is unchanged: from `sm` up the grid is the existing `auto-fill` of 15rem cards and the hero shows the featured camera.
- The shop front already used two columns on a phone; the catalogue now matches it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Below `sm` the catalogue grid has two columns with a smaller gap and price size.
- **FR-002**: Below `sm` the hero hides the featured photograph, its padding and heading shrink, and the category chips scroll in one row.
- **FR-003**: The price inputs are narrower below `sm` so Min – Max share a row.
- **FR-004**: An image shows a tinted, pulsing frame until it has loaded.
- **FR-005**: A browser test at 390px checks two products share the first row, the first product is within 1,700px of the top, and nothing scrolls sideways.

## Success Criteria *(mandatory)*

- **SC-001**: At 390px the twelve products take about half the height they did (checked in a browser).
- **SC-002**: The browser test fails with the old one-column grid.

## Assumptions

- 390px (an iPhone 12-15) is the reference phone width, as in the screen review.
