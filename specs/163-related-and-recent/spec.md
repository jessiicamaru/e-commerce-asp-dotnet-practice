# Feature Specification: Related products and recently viewed

**Feature Branch**: `feature/375-related-and-recent`
**Created**: 2026-10-09
**Status**: Draft
**Issue**: #375
**Input**: "Related products on the product page - same category, then the same department; recently viewed products,
kept in the browser, on the product page and the shop's landing page."

## Context

A product page ends at its reviews and questions; nothing leads on. A shopper on the wrong camera, shirt or pan goes
back to the listing and searches again, and one who looked at three products cannot find the first again without its
name. Found in the review of 2026-10-09.

## User Scenarios & Testing

### User Story 1 - Related products (Priority: P1)

Under a product, a row of others like it: the same category first, then the same department, never the product itself.

**Why this priority**: it turns a dead end into the next page, for every product, signed in or not.

**Independent Test**: open a camera in a category with five other cameras: the row shows those five, best reviewed
first, and not the camera itself.

**Acceptance Scenarios**:

1. **Given** a product in a category with others on the shelf, **When** its page opens, **Then** up to 8 of them are shown
   as cards, most reviewed first.
2. **Given** fewer than 8 in the category, **Then** the row is filled from the rest of its department.
3. **Given** a product off the shelf, or alone in its department, **Then** no row is drawn.
4. **Given** the row, **Then** each card reads like the listing's: language, currency, price, compare-at, availability,
   rating.

---

### User Story 2 - Recently viewed (Priority: P2)

The products this browser opened, most recent first, under the product page (without the current product) and on the
shop's landing page.

**Why this priority**: it gives back what was looked at; it needs no account.

**Independent Test**: open products A, B, C, then A again: the landing page reads A, C, B; A's page reads C, B.

**Acceptance Scenarios**:

1. **Given** products opened in order, **Then** the most recent comes first, each once, at most 12.
2. **Given** one of them taken off the shelf since, **Then** it is not shown.
3. **Given** nothing opened yet, **Then** no row is drawn.

### Edge Cases

- **Storage unavailable**: nothing is remembered and no row is drawn.
- **A deleted product** in the browser's list reads as absent and is dropped from the row.
- **The read cache** answers both reads anonymously (specs/157).

## Requirements

### Functional Requirements

- **FR-001**: `GET /api/products/{id}/related?limit=` (anonymous, 1-12, default 8) MUST answer other products on the shelf
  from the product's category, then its department, most reviewed first, never the product itself, in the listing's
  shape; an unknown or off-shelf product MUST answer an empty list, saying nothing about it.
- **FR-002**: The listing MUST accept `ids` (at most 24) and keep only those products, still only those on the shelf.
- **FR-003**: The storefront MUST remember the products opened (at most 12, most recent first, each once) in the browser
  and show them on the product page (without the current one) and the landing page, in the order remembered.
- **FR-004**: Both rows MUST use the listing's card.
- **FR-005**: Both reads MUST be cacheable anonymous reads (specs/157).

### Key Entities

- **Recently viewed**: product ids in this browser's storage - no names, no prices.

## Success Criteria

- **SC-001**: Related products come from the category first and the department second, never the product, never off the
  shelf (tested on PostgreSQL).
- **SC-002**: `ids` keeps exactly the products named that are on the shelf (tested).
- **SC-003**: In a browser, a product page shows related products, and after opening three products the landing page
  shows them most recent first.
- **SC-004**: Bruno covers both reads; `docs/reference` regenerated.

## Assumptions

- "Most reviewed" (rating count, then average) is the ranking; nothing like co-purchase data exists yet.
- Recently viewed is per browser, and not merged into an account (it is a convenience, not a record).
