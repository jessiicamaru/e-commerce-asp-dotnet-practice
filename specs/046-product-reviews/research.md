# Phase 0 Research: Ratings and reviews

> Written on 2026-09-27, after the feature merged (#98), from the code at that merge, the pull request and docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-24

The original plan recorded four decisions (D1-D4) in a sentence or two each; they are kept here word for word
under **Decision** and expanded. D5-D9 are decisions the code at the merge makes and explains in its own
comments, written out here so they are not left to be rediscovered. Where the original record names no rejected
alternative, the alternatives below are the ones the code and the PR argue against; who weighed them, and when,
is not recorded.

---

## D1 - Who may review: an eligibility read model fed by an event

**Decision**: An eligibility read model fed by an event, not a call to Order at write time. Catalog keeps
`review_eligibility` (product, customer, first delivered) from Order's `ParcelDeliveredEvent`, and the write
handler asks only that table.

**Rationale**: Catalog would otherwise depend on Order at review time. Being allowed to review is a fact that
only grows, so a copy that is seconds behind does no harm. This is unlike specs/031, where the question was
about ownership and needed to be answered live. There, a stale copy refuses a seller her own product with the
same 404 that means "not yours" - a permission that can be withdrawn must be read live. Here, the worst a copy
seconds behind can do is refuse a customer who confirmed their parcel a moment ago, and asking again a moment
later succeeds. Nothing is ever withdrawn, so a stale copy can never allow what should be refused.

**Alternatives considered**:

- **Ask Order over gRPC at write time ("did this customer receive this product?").** Rejected: a new synchronous
  edge from Catalog to Order, so reviewing fails whenever Order is down, for a question whose answer only ever
  changes from no to yes.
- **Let the client say which order the product came from.** Rejected: the order id would come from the request,
  and Catalog would still have to ask Order whether it is true - Principle IV's shape of defect.
- **Anybody signed in may review.** Rejected by the issue itself: "by customers who **received** it".

---

## D2 - What a review is of: products, not variants

**Decision**: Product ids, not variants. `ParcelDeliveredEvent.ProductIds` carries product ids, and
`product_reviews.ProductId` is the product.

**Rationale**: A review is of a camera, not of one kit option. Splitting reviews per variant would scatter a
product's few reviews across its shapes and give each an average from almost nothing. The contract says so in
its remarks: "a review is of a camera, not of its black body-only shape".

**Alternatives considered**:

- **Variant ids in the event and the review.** Rejected for the reason above.

---

## D3 - How the average is kept: recompute, never increment

**Decision**: Recompute, never increment. `SaveAndRecomputeAsync` saves what is staged and, in the same
transaction, sets `products.RatingCount` and `products.RatingAverage` from `count(*)` and
`round(avg("Rating")::numeric, 2)` over the product's rows with `"HiddenAt" IS NULL`.

**Rationale**: Two reviews landing at once would make an increment drift. Recomputing from the rows makes the
stored numbers a function of the visible reviews, whatever order changes arrive in; an edit, a hide and a
restore need no arithmetic of their own. Keeping the result on the product row means a listing shows stars
without counting anything.

**Alternatives considered**:

- **Increment `RatingCount` and a running sum on each write, adjust on edit, hide and restore.** Rejected: each
  of four paths has its own arithmetic to get right, and two concurrent writes lose one update.
- **Compute the average on every read.** Rejected: every listing page would aggregate reviews for every card;
  the row is where a listing already reads.

---

## D4 - What moderation does: hide, never delete

**Decision**: Hide, never delete. Hiding sets `HiddenAt`, `HiddenReason` (required) and `HiddenBy`; restoring
clears them. Both recompute the average.

**Rationale**: A moderator can be wrong, and the author's words stay on record. A restore is then possible
without asking the author to write again, and the audit log's before and after snapshots describe a row that
still exists.

**Alternatives considered**:

- **Delete the review.** Rejected: irreversible, and a wrong decision would cost the customer their words.

---

## D5 - The sweep locks what it sets

**Decision**: `SweepDeliveriesAsync` first selects the due parcels with `FOR UPDATE SKIP LOCKED`, then sets
exactly those with the same guard as before (`Status = Shipped AND DeliveredAt IS NULL`), and hands those ids to
the `stage` callback, which announces them.

**Rationale**: Before this, the sweep was one `UPDATE ... WHERE` that returned a count. To announce each parcel
it had to know which rows it actually set. Reading the due rows and then updating them without a lock would
announce a parcel a customer confirmed in between, twice. With the lock: a parcel whose row the customer's
confirmation holds is skipped and announced once, by the confirmation; a customer confirming a parcel the sweep
has locked waits, then fails the `DeliveredAt IS NULL` guard and announces nothing. `SKIP LOCKED` also keeps two
instances of the sweeper from waiting on each other.

**Alternatives considered**:

- **`UPDATE ... RETURNING "Id"`.** Equivalent in effect; not the shape chosen. Why is not recorded.
- **Announce from a separate job that looks for delivered parcels not yet announced.** Rejected: a new column
  or table to remember what was announced, and the announcement would no longer commit with the delivery.

---

## D6 - One event per parcel, not per order

**Decision**: `ParcelDeliveries.AnnounceAsync` publishes one `ParcelDeliveredEvent` per delivered parcel.
`GetDeliveredParcelsAsync` takes a parcel's products from the order lines whose `SellerId` equals the parcel's
`SellerId`; the shop's own parcel matches lines with no seller.

**Rationale**: Since specs/035 each seller ships their own part and each part is delivered on its own. A
customer who received half an order may review that half; an event per order would either wait for the last
parcel or announce products that have not arrived.

**Alternatives considered**:

- **One event per order when every parcel is delivered.** Rejected for the reason above.
- **The whole order's products on each parcel's event.** Rejected: it would let a customer review a product
  still in transit.

---

## D7 - The author's name: a `given_name` claim with a default

**Decision**: Identity writes `given_name` (the user's `FirstName`) into the access token. `ICurrentUser` gains
`string? GivenName => null` as a default interface member, and `CurrentUser` reads the claim. The handler copies
it onto the review; for a token without it, the first letter of the email and a dot.

**Rationale**: The name a review is signed with must come from the token like the customer's id (Principle IV),
and must be the first name only - never the surname or the email in public. The default member means every
existing test double and every service still compiles, and a token issued before the claim existed still works.

**Alternatives considered**:

- **Take the name from the request body.** Rejected: anybody could sign as anybody.
- **Ask Identity for the name at write time.** Rejected: a synchronous call for one string the token can carry.
- **Refuse a token without the claim.** Rejected: every customer signed in before the deploy would be refused
  until their token refreshed.

---

## D8 - One endpoint for writing and editing

**Decision**: `PUT /api/products/{productId}/reviews/mine` creates the caller's review or edits it; `GET` on the
same address answers `{ eligible, review }`. The unique index on (`ProductId`, `CustomerId`) backs it.

**Rationale**: "One review per customer per product" makes the review addressable by the product and the
caller, so there is no review id for the author to hold. A second write is an edit by construction, and the
storefront's form needs one call to know both whether to show itself and what to fill it with. Only a new review
notifies the seller and records `ReviewPosted`; an edit records `ReviewEdited`.

**Alternatives considered**:

- **`POST` to create and `PUT /reviews/{id}` to edit.** Rejected: a second `POST` would need its own 409, and the
  author would have to learn their review's id first.

---

## D9 - No backfill

**Decision**: Parcels delivered before this feature give no right to review.

**Rationale**: Catalog can learn who received what only from the event. Asking Order after the fact would add
the synchronous dependency D1 avoids, for a one-off.

**Alternatives considered**:

- **A one-off script replaying delivered parcels into `review_eligibility`.** Rejected: it would read Order's
  data into Catalog's database (Principle I) or need a republish mechanism nothing else uses.

---

## Open risks

| Risk | Impact | Mitigation |
| :-- | :-- | :-- |
| Two first reviews at the same instant | Both miss `GetMineAsync`, the second insert hits the unique index, the customer sees a 500 | Fixed in specs/057 (#127): `INSERT ... ON CONFLICT DO NOTHING`, the loser edits |
| Two moderators hide at once | Read-check-then-save lets both succeed and both audit | Fixed in specs/057: guarded single `UPDATE`, the loser gets 409 |
| A seller buys and reviews their own product | A review a shopper reads as independent is not | Fixed in specs/057: 403 "You cannot review your own product." |
| `review_eligibility` has no foreign key | Rows outlive a deleted product | Accepted; harmless, as no review can be written for a product that does not exist |
