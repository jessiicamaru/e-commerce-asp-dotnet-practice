# Feature Specification: Ratings and reviews

**Feature Branch**: `046-product-reviews` | **Created**: 2026-09-24 | **Issue**: #91

## Why

A shopper cannot see what other buyers thought of a camera, and a buyer has nowhere to say.

## User Scenarios

### US1 - A buyer reviews what they received (P1)

A customer whose parcel has arrived gives the product 1 to 5 stars and, if they like, some words. They
can do this once per product and edit it afterwards. The review is signed with their first name.

**Acceptance**
1. Somebody who has not received the product is refused with 403 and told why in words.
2. A second write edits the first review; it does not create another.

### US2 - Shoppers see what buyers thought (P1)

The product page shows the average, the count and the reviews, newest first. Listings show the average.

**Acceptance**
1. The average and count on the product match the reviews a shopper can see.

### US3 - Moderators hide what does not belong (P2)

A moderator hides a review, with a reason, and can restore it. A hidden review is not shown and not
counted. Each hide and restore goes in the audit log under Moderation.

### US4 - The seller hears about it (P3)

A seller is notified of a new review of their product. They are not notified of every edit.

## Requirements

- **FR-001**: Catalog learns who received what from an event Order publishes when a parcel is delivered,
  whether confirmed by the customer or taken as delivered automatically. There is one event per parcel,
  naming that parcel's products.
- **FR-002**: One review per customer per product, enforced by the database.
- **FR-003**: The average and the count are recomputed from the visible reviews in the same transaction
  as every change.
- **FR-004**: The author's name comes from the token, never from the request.

## Out of scope

- Parcels delivered before this feature give no right to review. There is no backfill: Catalog can only
  learn this from the event.
- Photos in reviews, replies from sellers, and "was this helpful" votes.

## Success Criteria

- **SC-001**: In one Bruno run, the customer whose parcel arrived reviews the product and edits the
  review; a moderator hides it and restores it; somebody else is refused.
- **SC-002**: Mutation checks fail if hidden reviews are counted or if the eligibility check is removed.
