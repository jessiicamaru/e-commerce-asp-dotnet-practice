# Feature Specification: Review races and own-product reviews

> Completed on 2026-09-27, after the feature merged (#140), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature Branch**: `057-review-races` | **Created**: 2026-09-24 | **Issue**: #127

**Status**: Merged (#140, 2026-09-24)

**Input**: Issue #127 - three problems in the ratings and reviews of specs/046: a concurrent first review is a
500, hiding and restoring are unguarded, and a seller can review their own product.

## Why

Three problems in the ratings and reviews of specs/046:

1. **Two first reviews at once give a 500.** A customer's double-click, or two tabs, both find "no
   review yet" and both insert. The unique index on `(ProductId, CustomerId)` refuses the second, and
   the exception reaches the customer as a 500.
2. **Hiding a review is not guarded.** Two moderators hiding the same review at once both read
   "visible", both save, and both write a `ReviewHidden` audit entry. Restoring has the same race.
   Every other moderation decision in this codebase is a guarded single-statement `UPDATE`.
3. **A seller can review their own product.** Eligibility comes from delivery, and nothing stops the
   seller who bought their own camera. That is a conflict of interest in the one signal shoppers read as
   independent.

## User Scenarios & Testing *(mandatory)*

### US1 - A double-posted first review is one review (Priority: P1)

A customer who presses "post" twice, or posts from two tabs, ends up with one review and sees no error.

**Why this priority**: A 500 on an ordinary double-click is a visible failure, and the seller could be told twice.

**Independent Test**: Send several first reviews of one product by one eligible customer at once; count reviews,
`ReviewPosted` entries and `NewReview` notices.

**Acceptance Scenarios**:

1. Several first reviews of one product by one customer, all at once, all succeed. Exactly one review
   exists, `ReviewPosted` is recorded once, and the seller is told once. The later writes edit it, as a
   second write always has.

---

### US2 - A review is hidden or restored once (Priority: P1)

**Why this priority**: A moderation decision must be taken once and recorded once; the audit log is how staff
decisions are checked.

**Independent Test**: Five staff hide one review at once; then five restore it at once.

**Acceptance Scenarios**:

1. Several moderators hiding one review at once: exactly one succeeds, the rest get 409, and there is
   one `ReviewHidden` entry.
2. The same holds for restoring.
3. The rating is recomputed in the same transaction as the change.

---

### US3 - Nobody reviews what they sell (Priority: P1)

**Why this priority**: A review is the one signal a shopper reads as independent; a seller rating their own
product defeats it.

**Independent Test**: Deliver a seller's own product to them, then try to review it and read `GET .../reviews/mine`.

**Acceptance Scenarios**:

1. A seller who received their own product cannot review it: 403, "You cannot review your own
   product."
2. They can review somebody else's product they received.
3. The page says the same: `GET .../reviews/mine` reports them not eligible.

### Edge Cases

- **The losing first write.** It inserts nothing, stages nothing, and edits the winning review with its own rating
  and text; the review ends with whichever write came last. Six concurrent writes produce one review, one
  `ReviewPosted`, one `NewReview`, and one `ReviewEdited` per losing write.
- **Hiding a review already hidden, or restoring a visible one, one at a time.** 409, as before.
- **A review of the shop's own product** (`SellerId` null): no seller to exclude and no seller to notify; unchanged.
- **The own-product check comes first.** A seller who has not received their own product is told "your own
  product", not "not eligible".

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The first review is inserted with `INSERT ... ON CONFLICT DO NOTHING`. Only a row that
  was actually inserted stages its audit entry and the seller's notice. A write that inserted nothing
  edits the review that won.
- **FR-002**: Hide and restore are `IReviewRepository.TryHideAsync` and `TryRestoreAsync`: one guarded
  `UPDATE`, with the audit entry staged inside its transaction, and the rating recomputed there too.
- **FR-003**: The own-product rule compares the product's `SellerId` with the caller, in the command
  and in the "mine" query.

### Key Entities

- **Review** (`product_reviews`, specs/046): one per customer per product (unique `(ProductId, CustomerId)`),
  hidden rather than deleted (`HiddenAt`, `HiddenReason`, `HiddenBy`).
- **Rating** (`products.RatingAverage`, `RatingCount`): recomputed from the visible rows, never incremented.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Six concurrent first reviews by one customer produce one review, one `ReviewPosted`, one `NewReview`,
  a rating count of 1, and no error.
- **SC-002**: Five concurrent hides produce exactly one success and four 409s, one `ReviewHidden`; the same for
  restores.
- **SC-003**: A seller's review of their own product is refused with 403, and "mine" reports not eligible.
- **SC-004**: The concurrency tests pass repeatedly (5 runs of 5 at the merge).

## Decision

**Retry as an edit, not 409.** The customer meant to review once. The second write of any review is
already an edit, so a race is resolved the way a slow second click would be.

Both this and the own-product rule are recorded in the pull request as decided on the user's behalf; see
[research.md](research.md).

## Assumptions

- Sellers also hold `Customer` (specs/027), which is how a seller could be eligible for their own product.
- Eligibility still comes only from delivery (`review_eligibility`, specs/046).

## Out of scope

- Reviews on a product that is off the shelf (later, #177).
