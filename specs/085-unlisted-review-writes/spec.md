# Feature Specification: No reviews off the shelf

> Completed on 2026-09-27, after the feature merged (#177), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature Branch**: `085-unlisted-review-writes` | **Created**: 2026-09-27 | **Status**: Merged (#177, 2026-09-26 UTC) | **Issue**: #174 (closes it)

**Input**: Issue #174 - specs/081 closed reading the reviews of a product off the shelf, but not writing them.

## Why

Since specs/081, a product off the shelf (pending, rejected or taken down) hides its reviews from everybody but
its seller and staff. Writing was still open. A customer who had once received the product could write or
change a review of it, and that moved the stored rating of a product nobody could see or buy.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A product off the shelf takes no review (Priority: P1)

A customer who received a camera reviewed it. The camera is then taken down. The customer tries to change their
review, or another customer who also received it tries to write one: both are told the product is not found, and
the product's rating does not move.

**Why this priority**: It is the whole issue. The rating is recomputed from the visible reviews on every write
(specs/046), so a write off the shelf changes a number that reappears the moment the product is back.

**Independent Test**: An eligible customer reviews a product on sale (rating 5); the product is taken down; a second
write is a 404 and the rating is still 5.

**Acceptance Scenarios**:

1. **Given** a product taken down, **When** an eligible customer writes or changes a review, **Then** the answer is
   404 `Product not found.` and nothing is stored.
2. **Given** the same product, **When** its rating is read afterwards, **Then** it is unchanged.
3. **Given** a product pending, rejected or inactive, **When** a review is written, **Then** the same 404.
4. **Given** a customer who never received a product taken down, **When** they write a review, **Then** the answer
   is the same 404 - not the 403 that would confirm the product exists (Bruno `seller/a review of it is a 404`,
   which was a 403 before this change).

---

### User Story 2 - The page says what the command would (Priority: P2)

On a product page, the review box asks `GET .../reviews/mine`. For a product off the shelf it reports the customer
not eligible, so the page offers no box the server would refuse.

**Why this priority**: The page only ever shows products its reader may see; this matters for the seller's and
staff's views of a product off the shelf, and for pages opened before a take-down. Second because the command is
already safe without it.

**Independent Test**: For the product above, `GET .../reviews/mine` as the eligible customer answers
`eligible: false`, with their existing review still returned.

**Acceptance Scenarios**:

1. **Given** a product off the shelf, **When** an eligible customer asks whether they may review, **Then**
   `eligible` is false.
2. **Given** a customer who already reviewed it, **When** they ask, **Then** their review is still returned.

---

### User Story 3 - Back on sale, reviews work again (Priority: P3)

The product is approved again. The customer changes their review, and the rating moves as before.

**Why this priority**: The gate must be a state, not a punishment; third because it only matters once the first
two hold.

**Independent Test**: Put the product back to `Approved`; a review of 4 is accepted and the rating becomes 4.

**Acceptance Scenarios**:

1. **Given** a product back on sale, **When** the customer writes a review of 4, **Then** it is stored and the
   rating is 4 from 1 review.

---

### Edge Cases

- **A product that does not exist.** The same 404 with the same words. Before this feature the words were
  `Product with ID '<id>' was not found.`; they are now `Product not found.` in both cases, so the two cannot be
  told apart.
- **The product's own seller.** Still refused (403 `You cannot review your own product.`, specs/057) - but only on
  sale; off the shelf the 404 comes first.
- **Staff.** Not customers; `PUT .../reviews/mine` is `Customer` only, unchanged.
- **Hiding and restoring reviews.** Staff moderation of existing reviews (specs/046) is not affected.
- **A product inactive but approved.** Refused too: the rule is `IsListed && IsActive`, what a shopper can buy.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** Writing or changing a review of a product that is not listed, or not active, is the 404
  `Product not found.`. That is the answer asking a question there already gives (specs/076), and the answer for
  a product that does not exist, so it confirms nothing.
- **FR-002** `GET /api/products/{id}/reviews/mine` reports the customer not eligible for such a product, so the
  page says what the command would.
- **FR-003** Back on sale, writing works again, and the rating moves as before.
- **FR-004** The 404 is decided before the own-product and eligibility checks.

### Key Entities

- **Review**: one per customer per product (specs/046); unchanged.
- **Product rating**: `RatingAverage` / `RatingCount`, recomputed from visible reviews on every write; what this
  feature protects.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Off the shelf, a review write is refused and the rating stays exactly where it was (5 from 1 review
  in the test).
- **SC-002**: Back on sale, a write is accepted and the rating moves (to 4 from 1 in the test).
- **SC-003**: A customer who never received the product gets the same 404 as anybody, where before this change they
  got 403 (Bruno seq 82).
- **SC-004**: Removing either gate - the write's or the eligibility's - turns the test red (two mutations).
- **SC-005**: No regression: Catalog 206/206, Bruno 268/268 requests and 438/438 tests at the merge.

## Assumptions

- "On sale" means `IsListed && IsActive`, the rule asking a question already uses (specs/076).
- A product off the shelf is still visible to its seller and staff (specs/081), so the page can still ask
  `reviews/mine` about it.

## Decisions

- **The same rule as asking a question**, `IsListed && IsActive`, not `ProductReview.MaySee`. MaySee lets the
  seller and staff *read*; neither is a customer who received the product, and a review is a customer's word.
- **The 404 comes before the eligibility check.** Before this, an ineligible customer got 403 "only a customer who
  has received this product can review it", which confirmed the product exists.

The reasoning is expanded in [research.md](./research.md).

## Out of scope

- Hiding existing reviews when a product is taken down (they are already hidden from the public by specs/081).
- Freezing the rating shown to the seller and staff.
