# Implementation Plan: Ratings and reviews

**Branch**: `046-product-reviews` | **Spec**: [spec.md](spec.md)

## Technical Context

- **Contract**: `Order/ParcelDeliveredEvent(OrderId, ShipmentId, BuyerId, ProductIds, DeliveredAt)`.
- **Order**
  - `ParcelDeliveries.AnnounceAsync` publishes one event per delivered parcel, inside the delivery's own
    transaction:
    - for the customer's confirmation, through the existing `stage`;
    - for the auto-confirm sweep, through a `stage` that now receives the shipment ids.
  - The sweep now locks its rows (`FOR UPDATE SKIP LOCKED`) before setting them. The ids it announces
    are therefore exactly the ones it delivered. A parcel the customer confirmed during the sweep is
    announced once, by the customer's confirmation.
  - `GetDeliveredParcelsAsync` finds a parcel's products: the order lines whose seller matches the
    parcel's seller. The shop's own part matches lines with no seller.
- **Identity**: tokens carry `given_name`. `ICurrentUser.GivenName` has a default of null, so test doubles
  and old tokens still compile and work.
- **Catalog**
  - Tables:
    - `review_eligibility`, with primary key (ProductId, CustomerId), filled
      `ON CONFLICT DO NOTHING`.
    - `product_reviews`, unique on (ProductId, CustomerId), with a CHECK that rating is between 1 and 5.
  - `products` gains `RatingAverage numeric(3,2)` and `RatingCount`.
  - `ReviewEligibilityConsumer` feeds the eligibility table.
  - `ReviewHandlers` covers writing, reading, and staff hide and restore.
  - `SaveAndRecomputeAsync` saves and recomputes the average and count from the visible rows, in one
    transaction.
  - Routes:
    - `GET /api/products/{id}/reviews` (public).
    - `GET` and `PUT /api/products/{id}/reviews/mine` (the caller's own review).
    - `GET /api/reviews` and `POST /api/reviews/{id}/hide|restore` (Staff).
- **Gateway**: `/api/reviews/**` → catalog.
- **Client**
  - `StarRating` and `StarInput`.
  - `ProductReviews` on the product page: the average, the list, and a form offered only when the server
    says the customer is eligible.
  - The average on the product card.
  - `/admin/reviews` for staff.
  - Notification wording for `NewReview`.

## Research

- **D1 - An eligibility read model fed by an event, not a call to Order at write time.** Catalog would
  otherwise depend on Order at review time. Being allowed to review is a fact that only grows, so a copy
  that is seconds behind does no harm. This is unlike specs/031, where the question was about ownership
  and needed to be answered live.
- **D2 - Product ids, not variants.** A review is of a camera, not of one kit option.
- **D3 - Recompute, never increment.** Two reviews landing at once would make an increment drift.
- **D4 - Hide, never delete.** A moderator can be wrong, and the author's words stay on record.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I | Order says what was delivered. Catalog owns reviews and who may write them. |
| III | The event is staged in the delivery transaction; eligibility is idempotent by key; a review commits with its audit entry, its notification and the recomputed average. |
| IV | The customer and their name come from the token; eligibility comes from Catalog's own table. |
| V | Integration tests in Order and Catalog, 2 mutation checks, Bruno, and client tests. |
