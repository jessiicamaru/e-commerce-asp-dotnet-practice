# Research: No reviews off the shelf

> Written on 2026-09-27, after the feature merged (#177), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-27

Three decisions. D1 and D2 are the spec's Decisions; D3 is read from the diff.

---

## D1 - "On sale" (`IsListed && IsActive`), not `ProductReview.MaySee`

**Decision**: the write and the eligibility ask `OnSale(product) = product.IsListed && product.IsActive` - what a
shopper can buy, the rule asking a question already uses (specs/076).

**Rationale**: `MaySee` lets the seller and staff *read* a product off the shelf; neither is a customer who received
it, and a review is a customer's word. Using `MaySee` would open the write off the shelf to exactly the people it
admits - its seller (refused anyway as the product's own seller) and staff (who reach the endpoint only if they also
hold `Customer`, and then only with a received parcel) - which says the wrong thing about who the rule is for.

**Alternatives considered**:

- **`ProductReview.MaySee`.** Rejected for the reason above.
- **`IsListed` alone.** Not recorded as considered; `IsActive` is part of "on sale" everywhere else a shopper acts.

---

## D2 - The 404 comes first

**Decision**: the order of checks in `WriteReviewCommand` is: exists and on sale (404), own product (403), received
it (403).

**Rationale**: before this change an ineligible customer got 403 "Only a customer who has received this product can
review it.", which confirmed the product exists. Bruno's new request (seq 82) is exactly that customer, and it was a
403 before this change.

**Alternatives considered**: keeping the eligibility check first - rejected for the reason above.

---

## D3 - One wording for "not there" and "not on sale"

**Decision**: the missing-product message of `WriteReviewCommand` changed from `Product with ID '<id>' was not
found.` to `Product not found.`, the same words as off the shelf.

**Rationale**: two different messages would let a caller tell a real id from a made-up one - the thing the 404 is
there to hide (#28's rule, applied throughout specs/081).

**Alternatives considered**: none recorded.
