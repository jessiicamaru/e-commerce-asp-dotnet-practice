# Feature Specification: A shop's rating

**Feature Branch**: `feature/379-shop-rating`
**Created**: 2026-10-09
**Status**: Draft
**Issue**: #379
**Input**: "A shop's rating - its reviews summed up on its page and beside its name on a product."

## Context

A shopper deciding whether to buy from a seller sees each product's stars but not the shop's. A shop whose products carry
300 reviews averaging 4.8 and a shop with none read the same, both on the shop's page and in the product page's "Sold by".
The seller already sees this number on their own insights page (specs/068); nobody else does. Found in the review of
2026-10-09.

## User Scenarios & Testing

### User Story 1 - The shop page says how its products are rated (Priority: P1)

Under the shop's name: its average rating and how many reviews it rests on, or "no reviews yet".

**Why this priority**: the shop page is where a shopper sizes up a seller.

**Independent Test**: a shop with one product reviewed 4 once and another reviewed 2 three times shows 2.5 from 4 reviews.

**Acceptance Scenarios**:

1. **Given** a shop whose products have visible reviews, **When** its page opens, **Then** it shows the average of every one
   of those reviews (to one decimal) and their count.
2. **Given** a shop with no visible review, **Then** the page says it has no reviews yet - never zero stars.
3. **Given** a review hidden by a moderator, **Then** it no longer counts.

---

### User Story 2 - The product page shows the shop's rating beside its name (Priority: P2)

"Sold by Mai Lens ★ 4.8 (300)" under a seller's product.

**Why this priority**: the decision to buy is made on the product page; the shop's record belongs where the shop is named.

**Independent Test**: open a product of the shop above: the line beside "Sold by" reads 2.5 and 4 reviews.

**Acceptance Scenarios**:

1. **Given** a seller's product, **Then** the shop's rating is shown beside the shop's name, when it has any review.
2. **Given** the shop's own product (no seller), **Then** nothing is shown - it has no shop page and no shop rating.
3. **Given** the shop's page cannot be read (closed, unknown), **Then** the product page shows the name alone.

### Edge Cases

- **A product taken off the shelf** keeps counting: its reviews are the shop's history, and withdrawing a product must not
  wash away its bad reviews. A deleted product (staff only, specs/024) takes its reviews with it.
- **The seller's own insights** show the same number: one computation for both.
- **The read cache** (specs/157): a review written, hidden or restored recomputes `products`, which evicts the cache.

## Requirements

### Functional Requirements

- **FR-001**: `GET /api/shops/{id}` MUST carry `ratingAverage` (null when there is no visible review, else rounded to two
  decimals) and `ratingCount`: every visible review of every product the seller has, averaged.
- **FR-002**: The seller's insights (`/api/products/insights/mine`) and the shop page MUST compute it in one place.
- **FR-003**: The shop page MUST show the average and the count, or "no reviews yet".
- **FR-004**: The product page MUST show the shop's average and count beside "Sold by" for a seller's product with
  reviews, and nothing for the shop's own product.

### Key Entities

- **Shop rating**: derived on read from `products.RatingAverage` and `RatingCount` - nothing stored.

## Success Criteria

- **SC-001**: The average is weighted by each product's count, counts hidden reviews out and keeps off-shelf products in
  (tested on PostgreSQL).
- **SC-002**: The shop page and the seller's insights give the same number for the same shop (tested).
- **SC-003**: In a browser, the shop page and a product page show the rating.
- **SC-004**: Bruno checks the field; `docs/reference` regenerated.

## Assumptions

- The rating is about the shop's products, since nothing in the system collects a rating of the seller itself
  (delivery, answers). Rating sellers separately would be its own feature.
- No minimum number of reviews before showing: the count is shown beside it, which lets the reader judge.
