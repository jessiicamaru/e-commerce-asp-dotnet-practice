# Feature Specification: Ratings and reviews

> Completed on 2026-09-27, after the feature merged (#98), from the code at that merge, the pull request and docs/features/ratings-and-reviews.md.

**Feature Branch**: `046-product-reviews` | **Created**: 2026-09-24 | **Issue**: #91

**Status**: Merged - PR #98, merge commit `c8a64d5`, 2026-09-24 (+07:00)

**Input**: Issue #91, "customers rate and review what they received": ratings (1-5 stars, optional text) by
customers who **received** the product, learnt by Catalog from an event Order publishes when a parcel is
delivered (specs/040); one review per customer per product, editable by its author; the average, the count and
the reviews on the product page and the average on listings; a moderator hides a review with a reason and
restores it, and hidden reviews leave the average; the seller notified of a new review; audit entries
(Moderation) for hide and restore. Acceptance: someone who has not received the product cannot review it
(403/409, in words); the average matches the visible reviews; a hidden review is not shown and not counted.

## Why

A shopper cannot see what other buyers thought of a camera, and a buyer has nowhere to say.

## Context

Until this feature the shop had no word from buyers at all. Two facts shaped it. First, Catalog owns products
but does not know who bought what: orders, parcels and deliveries are Order's. Second, specs/040 had just given
the system a moment at which a customer has, beyond doubt, received something - a parcel confirmed by its
customer, or taken as delivered seven days after it shipped. That moment is what makes a review worth reading:
it is written by somebody who has held the camera. So the feature has two halves - Order says what was
delivered, Catalog keeps who may review and the reviews themselves.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A buyer reviews what they received (Priority: P1)

A customer whose parcel has arrived gives the product 1 to 5 stars and, if they like, some words. They
can do this once per product and edit it afterwards. The review is signed with their first name.

**Why this priority**: Without it there is nothing to show; every other story reads or moderates what this one
writes. It is also where the rule that matters lives - only somebody who received the product.

**Independent Test**: Confirm a parcel as its customer, then write a review of a product in it; write again and
observe the same review changed rather than a second one. Try the same as a customer who never received the
product and observe a 403 that says why.

**Acceptance Scenarios**:

1. Somebody who has not received the product is refused with 403 and told why in words.
2. A second write edits the first review; it does not create another.
3. **Given** a customer whose parcel containing the product has been delivered (confirmed by them or taken as
   delivered by the sweep), **When** they ask whether they may review it, **Then** the answer is yes and they
   have no review yet.
4. **Given** that customer, **When** they give 5 stars and some words, **Then** the review is saved, signed with
   the first name their sign-in carries - never a name sent with the review.
5. **Given** a rating outside 1 to 5, or words longer than 2000 characters, **When** it is sent, **Then** it is
   refused as invalid and nothing is saved.
6. **Given** a customer who received only one seller's parcel of a two-seller order, **When** they ask about a
   product in the other parcel, **Then** they may not review it yet.

---

### User Story 2 - Shoppers see what buyers thought (Priority: P1)

The product page shows the average, the count and the reviews, newest first. Listings show the average.

**Why this priority**: Equal first with US1, because a review nobody can read delivers nothing; this is the half
the issue's opening sentence is about.

**Independent Test**: With two visible reviews of 4 and 2 stars, read the product and its reviews without
signing in: average 3.00, count 2, both reviews listed newest first.

**Acceptance Scenarios**:

1. The average and count on the product match the reviews a shopper can see.
2. **Given** a product with no visible reviews, **When** it is read, **Then** it has no average (null) and a
   count of 0, and no stars are drawn.
3. **Given** a listing page, **When** a product on it has reviews, **Then** its card shows the average and the
   count.
4. **Given** a review that was edited more than a second after it was written, **When** it is listed, **Then**
   it is marked as edited.

---

### User Story 3 - Moderators hide what does not belong (Priority: P2)

A moderator hides a review, with a reason, and can restore it. A hidden review is not shown and not
counted. Each hide and restore goes in the audit log under Moderation.

**Why this priority**: Needed as soon as reviews are public - an advertisement or abuse in a review is visible to
every shopper - but it only matters once US1 and US2 exist.

**Independent Test**: With reviews of 1 and 5 stars, hide the 1-star one: the product reads 5.00 from 1 review
and the page no longer lists it; restore it: 3.00 from 2.

**Acceptance Scenarios**:

1. **Given** a visible review, **When** a moderator or administrator hides it with a reason, **Then** it leaves
   the product page and the average, and appears in the staff list of hidden reviews with the reason.
2. **Given** a hide with no reason, **When** it is sent, **Then** it is refused as invalid.
3. **Given** a hidden review, **When** it is hidden again, **Then** the answer is 409 and nothing changes.
4. **Given** a hidden review, **When** it is restored, **Then** it is back on the page and in the average.
5. **Given** a customer, **When** they try to hide a review, **Then** they are refused with 403.

---

### User Story 4 - The seller hears about it (Priority: P3)

A seller is notified of a new review of their product. They are not notified of every edit.

**Why this priority**: Useful, not essential - the review is on the page whether or not the seller is told.

**Independent Test**: Review a seller's product and then edit the review: the seller has exactly one `NewReview`
notice, carrying the product's name and the first rating.

**Acceptance Scenarios**:

1. **Given** a seller's product, **When** a customer reviews it for the first time, **Then** the seller is sent
   one notice naming the product and the number of stars.
2. **Given** that review, **When** its author edits it, **Then** no further notice is sent.
3. **Given** a product of the shop itself (no seller), **When** it is reviewed, **Then** nobody is notified.

---

### Edge Cases

- **The same product delivered twice to one customer** (a redelivered event, or a second parcel of the same
  camera) is one right to review, not two.
- **An order split between sellers.** Each parcel says what was in it; receiving one seller's parcel gives no
  right to review the other seller's products.
- **The customer confirms a parcel while the 7-day sweep is taking it as delivered.** The parcel is announced
  once, by whichever of the two actually set it.
- **A customer confirms the same parcel twice.** The second confirmation changes nothing and announces nothing.
- **A token issued before sign-ins carried a first name.** The review is signed with the first letter of the
  email and a dot, rather than refused or left unsigned.
- **A product with every review hidden** reads as a product with no reviews: no average, count 0.
- **A product deleted** takes its reviews with it; the record of who received it stays (see data-model.md).
- **Two first reviews at the same instant** (double-click, two tabs): at this merge both reach the unique index
  and the second is a 500 - found afterwards and fixed in specs/057 (#127), not handled here.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Catalog learns who received what from an event Order publishes when a parcel is delivered,
  whether confirmed by the customer or taken as delivered automatically. There is one event per parcel,
  naming that parcel's products.
- **FR-002**: One review per customer per product, enforced by the database.
- **FR-003**: The average and the count are recomputed from the visible reviews in the same transaction
  as every change.
- **FR-004**: The author's name comes from the token, never from the request.
- **FR-005**: A review may be written only by a customer Catalog has recorded as having received the product;
  anybody else MUST be refused with 403 and a sentence saying why.
- **FR-006**: A second write by the same customer for the same product MUST edit the existing review.
- **FR-007**: A rating MUST be a whole number from 1 to 5 - checked by the validator and by a database CHECK -
  and the words, optional, at most 2000 characters.
- **FR-008**: Anybody, signed in or not, MUST be able to read a product's visible reviews, newest first, paged.
- **FR-009**: A signed-in person MUST be able to ask whether they may review a product and read their own
  review, so the storefront offers the form only when the server says so.
- **FR-010**: Staff (Admin or Moderator) MUST be able to list visible or hidden reviews across the shop, with the
  product's name, and hide a review with a reason or restore it. A hidden review is never deleted.
- **FR-011**: A hidden review MUST NOT be shown to shoppers and MUST NOT count toward the average or the count.
- **FR-012**: Every write, edit, hide and restore MUST be recorded in the audit log - writing and editing under
  Catalog, hiding and restoring under Moderation - committed with the change.
- **FR-013**: The product's seller MUST be notified of a new review, and MUST NOT be notified of an edit.
- **FR-014**: The delivery announcement MUST be committed with the delivery itself, and a parcel MUST be
  announced at most once whichever path delivered it.
- **FR-015**: The average MUST be carried on the product so that a listing shows it without counting reviews.

### Key Entities

- **Right to review (eligibility)**: that one customer received one product, and when first. Only grows.
  Recorded by Catalog from Order's delivery announcements.
- **Review**: one customer's rating of one product - stars, optional words, the first name it is signed with,
  when written and last changed, and, if hidden, when, by whom and why.
- **Product rating**: the average of the visible reviews (two decimal places, none when there are none) and how
  many there are, kept on the product.
- **Parcel delivered (message)**: one delivered parcel - its order, the buyer, the products in it and when.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In one Bruno run, the customer whose parcel arrived reviews the product and edits the
  review; a moderator hides it and restores it; somebody else is refused.
- **SC-002**: Mutation checks fail if hidden reviews are counted or if the eligibility check is removed.
- **SC-003**: For every product, the stored average and count equal the average and count of its visible
  reviews after every write, hide and restore - asserted by `ReviewTests` for write, edit, hide and restore.
- **SC-004**: One delivered parcel produces exactly one announcement naming exactly its own products, on both
  delivery paths, even when the confirmation or the sweep is repeated - asserted by two `DeliveryTests`.
- **SC-005**: A seller's product reviewed once and edited once produces exactly one notice to the seller.

## Assumptions

- Delivery (specs/040) exists and is the only moment a customer is known to have received something.
- Reviews are of products, not of variants: a black body-only camera and the same camera with a kit lens are
  one product to review.
- The first name is the name a customer gave at registration; it is copied onto the review when first written
  and does not follow a later rename.
- Moderators are the staff of specs/043 (Admin or Moderator); no new role is needed.
- Notifications (specs/042) and the audit log (specs/041) exist and are reused as they are.

## Out of scope

- Parcels delivered before this feature give no right to review. There is no backfill: Catalog can only
  learn this from the event.
- Photos in reviews, replies from sellers, and "was this helpful" votes.
- Stopping a seller from reviewing their own product (added in specs/057), telling a reviewer their review was
  hidden (specs/059), and hiding the reviews of a product that is off the shelf (specs/081, specs/085) - all
  later work, none of it in this feature.
