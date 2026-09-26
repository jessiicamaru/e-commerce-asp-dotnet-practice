# Implementation Plan: Ratings and reviews

> Completed on 2026-09-27, after the feature merged (#98), from the code at that merge, the pull request and docs/features/ratings-and-reviews.md.

**Branch**: `046-product-reviews` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/046-product-reviews/spec.md`

## Summary

Buyers rate and review what they received. Order publishes a new `ParcelDeliveredEvent` - one per delivered
parcel, naming that parcel's products - inside the transaction that marks the parcel delivered, on both
delivery paths of specs/040. Catalog keeps a `review_eligibility` read model from it, and owns
`product_reviews`: one per customer per product, a second write editing the first, signed with the token's new
`given_name` claim. The product carries `RatingAverage` and `RatingCount`, recomputed from the visible rows in
the transaction of every write, hide and restore. Staff hide (with a reason) and restore; the seller is told of
a new review. The storefront gets stars, a reviews section, the average on cards and `/admin/reviews`.

One thing the issue did not anticipate surfaced during design and is folded in: **the auto-confirm sweep could
not say which parcels it had delivered.** It was a single `UPDATE ... WHERE` returning a count; to announce each
parcel it must know the ids it actually set, so it now locks the due rows first (`FOR UPDATE SKIP LOCKED`) and
sets exactly those (research D5).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript / React 19 in `client/`

**Primary Dependencies**: MassTransit 8.3.6 (EF outbox), MediatR 12.4.1, FluentValidation 12.1.1, Npgsql EF
Core; `Ecommerce.Shared` (`IAuditTrail`, `INotifier`, `ICurrentUser`, `ForbiddenException`); TanStack Query,
shadcn/ui in the storefront

**Storage**: PostgreSQL - `ecommerce_catalog_db` (5433) gains two tables and two columns; `ecommerce_order_db`
(5434) is unchanged in schema

**Testing**: xUnit against a real PostgreSQL (Catalog 5433, Order 5434) with the MassTransit test harness for
published messages; Vitest for the client; Bruno through the gateway; `verify-saga.sh`

**Target Platform**: the existing Catalog (5057), Order (5059) and Identity (5056) services behind the gateway
(5000); no new service

**Performance Goals**: none stated. The one design choice made for speed is keeping the average on the product
row so a listing reads it without counting (D3)

**Constraints**: the delivery announcement commits with the delivery (Principle III); a parcel is announced at
most once; the average always equals the visible rows; the author's identity and name come from the token

**Scale/Scope**: not recorded beyond the project's usual single instance per service

### Design by service

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
- **Gateway**: `/api/reviews/**` → catalog. Two routes on `catalog-cluster`: `catalog-reviews-route`
  (`/api/reviews/{**catch-all}`) and `catalog-reviews-root-route` (`/api/reviews`, the staff list, which a
  catch-all alone does not match). `/api/products/**` already reached Catalog.
- **Client**
  - `StarRating` and `StarInput`.
  - `ProductReviews` on the product page: the average, the list, and a form offered only when the server
    says the customer is eligible.
  - The average on the product card.
  - `/admin/reviews` for staff.
  - Notification wording for `NewReview`.

## Research

The decisions in full, with rejected alternatives, are in [research.md](research.md). In short:

- **D1 - An eligibility read model fed by an event, not a call to Order at write time.** Catalog would
  otherwise depend on Order at review time. Being allowed to review is a fact that only grows, so a copy
  that is seconds behind does no harm. This is unlike specs/031, where the question was about ownership
  and needed to be answered live.
- **D2 - Product ids, not variants.** A review is of a camera, not of one kit option.
- **D3 - Recompute, never increment.** Two reviews landing at once would make an increment drift.
- **D4 - Hide, never delete.** A moderator can be wrong, and the author's words stay on record.
- **D5 - The sweep locks what it sets**, so it announces exactly the parcels it delivered.
- **D6 - One event per parcel**, not per order.
- **D7 - The name comes from a new `given_name` claim**, with a default on `ICurrentUser`.
- **D8 - One upsert-shaped endpoint** (`PUT .../reviews/mine`) for writing and editing.
- **D9 - No backfill.**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. The original plan's table
had rows for I, III, IV and V; its sentences are kept below, with II added and each verdict stated.

| Principle | Verdict |
| :-- | :-- |
| **I. Service Autonomy** | **Pass.** Order says what was delivered. Catalog owns reviews and who may write them. Catalog never reads Order's database and never calls Order: it keeps its own copy of who received what, and that copy *does* inform an allow/deny decision - which the principle permits because Catalog is the owner of the fact "may review" and builds it from Order's announcement, not a display copy of an Order fact (research D1) |
| **II. Clean Architecture Layering** | **Pass.** `IReviewRepository` is declared in Catalog's `Application/Common/Interfaces/` and implemented in Infrastructure; `ReviewEligibilityConsumer` and `ReviewsController` live in WebApi and only dispatch through MediatR; Order's announcement is Application code over `IPublishEndpoint` from `MassTransit.Abstractions`. One deviation from the foldering convention, recorded rather than hidden: all review requests and handlers sit in one file, `Application/Reviews/ReviewFeatures.cs`, not a folder per use case |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The event is staged in the delivery transaction; eligibility is idempotent by key; a review commits with its audit entry, its notification and the recomputed average. The customer's confirmation stages it in the transaction of the guarded `UPDATE` (only when that update changed the row); the sweep stages it in its own transaction after locking. The consumer's insert is `ON CONFLICT DO NOTHING` on the primary key |
| **IV. Identity Comes From the Token** | **Pass.** The customer and their name come from the token; eligibility comes from Catalog's own table. `WriteReviewCommand` carries no user id and no name; `BuyerId` in the event comes from `orders.UserId`, which checkout took from the token. `GET .../reviews` is `[AllowAnonymous]` explicitly; everything else is authenticated, staff routes by `StaffRoles.Staff` |
| **V. Evidence Over Assumption** | **Pass.** Integration tests in Order and Catalog, 2 mutation checks, Bruno, and client tests. The tests run against real PostgreSQL because the guarantees are the database's (the unique key, `ON CONFLICT`, the row lock); the PR records Order 176/176, Catalog 149/149, Identity 71/71, client 221/221, Bruno 180/180 requests, and `verify-saga.sh` passing |

**Post-Phase 1 re-check**: no violations. Two things were weighed and are recorded rather than waived: the
eligibility read model deciding a permission (Principle I - acceptable because the permission is Catalog's and
only grows, D1), and the one-file handler set (Principle II's foldering convention, not its dependency rule).
Neither needed a Complexity Tracking entry.

What the check did **not** catch, found after the merge: hiding was read-check-then-save rather than a guarded
update, so two moderators at once could both hide and both be audited, and two first reviews at once hit the
unique index as a 500. Principle III's "a repeated attempt affects zero rows" was met for the delivery and the
eligibility, not for the review writes. Fixed in specs/057 (#127).

## Project Structure

### Documentation (this feature)

```text
specs/046-product-reviews/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Decisions D1-D9 with rejected alternatives
├── data-model.md        # Tables, columns, indexes, the migration
├── quickstart.md        # Validation scenarios
├── contracts/
│   ├── http-api.md      # Review endpoints, gateway routes, the given_name claim
│   └── messages.md      # ParcelDeliveredEvent and what Catalog publishes
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/
├── Ecommerce.Contracts/Order/ParcelDeliveredEvent.cs                      # new contract
└── Ecommerce.Shared/
    ├── Authentication/ICurrentUser.cs, CurrentUser.cs                     # GivenName, default null
    └── Notifications/Notifier.cs                                          # NotificationKind.NewReview

server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Security/JwtTokenGenerator.cs   # given_name claim

server/src/Services/Order/
├── Ecommerce.Order.Application/Common/Interfaces/IOrderRepository.cs     # stage gets ids; GetDeliveredParcelsAsync; DeliveredParcel
├── Ecommerce.Order.Application/Orders/Commands/ConfirmDelivery/DeliveryCommands.cs   # ParcelDeliveries.AnnounceAsync, both handlers
└── Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs       # sweep locks FOR UPDATE SKIP LOCKED

server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/Review.cs                            # Review, ReviewEligibility
├── Ecommerce.Catalog.Domain/Entities/Product.cs                           # RatingAverage, RatingCount
├── Ecommerce.Catalog.Application/Common/Interfaces/IReviewRepository.cs
├── Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs                # requests, validators, ReviewHandlers
├── Ecommerce.Catalog.Application/Products/Common/ProductResponse.cs       # ratingAverage, ratingCount
├── Ecommerce.Catalog.Infrastructure/Configurations/ReviewConfiguration.cs # both tables
├── Ecommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs
├── Ecommerce.Catalog.Infrastructure/Persistence/CatalogDbContext.cs
├── Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ReviewRepository.cs   # SaveAndRecomputeAsync
├── Ecommerce.Catalog.Infrastructure/DependencyInjection.cs
├── Ecommerce.Catalog.Infrastructure/Migrations/20260923212320_AddProductReviews.cs
├── Ecommerce.Catalog.WebApi/Consumers/ReviewEligibilityConsumer.cs
├── Ecommerce.Catalog.WebApi/Controllers/ReviewsController.cs
└── Ecommerce.Catalog.WebApi/Program.cs                                    # AddConsumer<ReviewEligibilityConsumer>

server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json                # two review routes

server/tests/
├── Ecommerce.Catalog.Tests/ReviewTests.cs, CatalogTestFixture.cs
└── Ecommerce.Order.Tests/DeliveryTests.cs

client/src/
├── components/product/star-rating/, product-reviews/, product-card/
├── pages/product/, pages/admin-reviews/
├── services/review/ (index.ts, types.ts), services/product/types.ts
├── hooks/review/, constants/query-keys/
├── layouts/admin-layout/, routes/
└── locales/{en,vi}/{catalog,admin,notifications}.json

bruno/reviews/ (10 requests + folder.yml), bruno/seller/someone who did not receive it cannot review it.yml,
bruno/security-checks/reviewing without a token is 401.yml
```

**Structure Decision**: No new project. Reviews are a Catalog aggregate beside products; the announcement is a
few lines in Order's existing delivery commands; the claim is one line in Identity. No `.csproj` was added, so
the Dockerfile did not change.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- **Parcels delivered before it give no right to review** - there is no backfill (research D9).
- **Races in the review writes themselves**: two first reviews at once answer 500 on the second, and two hides
  at once both succeed and both audit. Found afterwards and fixed in specs/057 (#127).
- **A seller who received their own product can review it.** Nothing here stops it; specs/057 does.
- **The reviewer is not told that their review was hidden** - added in specs/059.
- **A product off the shelf** still serves its reviews publicly and still accepts new ones - closed by specs/081
  and specs/085 (#177).
- **`review_eligibility` has no foreign key**, so its rows outlive a deleted product, while the reviews cascade
  away with it.
- The staff list has no paging validator (only the public list does).
- No photos, seller replies or helpfulness votes (spec, Out of scope).
