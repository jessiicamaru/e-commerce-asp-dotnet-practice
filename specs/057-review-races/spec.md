# Feature Specification: Review races and own-product reviews

**Feature Branch**: `057-review-races` | **Created**: 2026-09-24 | **Issue**: #127

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

## User Scenarios

### US1 - A double-posted first review is one review (P1)

**Acceptance**
1. Several first reviews of one product by one customer, all at once, all succeed. Exactly one review
   exists, `ReviewPosted` is recorded once, and the seller is told once. The later writes edit it, as a
   second write always has.

### US2 - A review is hidden or restored once (P1)

**Acceptance**
1. Several moderators hiding one review at once: exactly one succeeds, the rest get 409, and there is
   one `ReviewHidden` entry.
2. The same holds for restoring.
3. The rating is recomputed in the same transaction as the change.

### US3 - Nobody reviews what they sell (P1)

**Acceptance**
1. A seller who received their own product cannot review it: 403, "You cannot review your own
   product."
2. They can review somebody else's product they received.
3. The page says the same: `GET .../reviews/mine` reports them not eligible.

## Requirements

- **FR-001**: A unique violation on the first insert discards the failed write and retries once as an
  edit. The failed attempt's staged audit entry and notification are discarded with it.
- **FR-002**: Hide and restore are `IReviewRepository.TryHideAsync` and `TryRestoreAsync`: one guarded
  `UPDATE`, with the audit entry staged inside its transaction, and the rating recomputed there too.
- **FR-003**: The own-product rule compares the product's `SellerId` with the caller, in the command
  and in the "mine" query.

## Decision

**Retry as an edit, not 409.** The customer meant to review once. The second write of any review is
already an edit, so a race is resolved the way a slow second click would be.
