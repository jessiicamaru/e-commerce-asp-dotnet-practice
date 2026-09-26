# Feature Specification: No reviews off the shelf

**Feature Branch**: `085-unlisted-review-writes` | **Created**: 2026-09-27 | **Issue**: #174 (closes it)

## Why

Since specs/081, a product off the shelf (pending, rejected or taken down) hides its reviews from everybody but
its seller and staff. Writing was still open. A customer who had once received the product could write or
change a review of it, and that moved the stored rating of a product nobody could see or buy.

## Requirements

- **FR-001** Writing or changing a review of a product that is not listed, or not active, is the 404
  `Product not found.`. That is the answer asking a question there already gives (specs/076), and the answer for
  a product that does not exist, so it confirms nothing.
- **FR-002** `GET /api/products/{id}/reviews/mine` reports the customer not eligible for such a product, so the
  page says what the command would.
- **FR-003** Back on sale, writing works again, and the rating moves as before.

## Decisions

- **The same rule as asking a question**, `IsListed && IsActive`, not `ProductReview.MaySee`. MaySee lets the
  seller and staff *read*; neither is a customer who received the product, and a review is a customer's word.
- **The 404 comes before the eligibility check.** Before this, an ineligible customer got 403 "only a customer who
  has received this product can review it", which confirmed the product exists.
