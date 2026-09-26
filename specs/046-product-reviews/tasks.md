---
description: "Task list for Ratings and reviews"
---

# Tasks: Ratings and reviews

> Completed on 2026-09-27, after the feature merged (#98), from the code at that merge, the pull request and docs/features/ratings-and-reviews.md.

**Input**: Design documents from `/specs/046-product-reviews/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The guarantees are the database's (the unique key, `ON CONFLICT`, the sweep's row lock) and
the outbox's (the event commits with the delivery), so they run against a real PostgreSQL with the MassTransit
test harness (constitution Principle V).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US4)

## As originally recorded

The eight tasks of the original list, kept as they were written and ticked:

- [X] T001 Contract `ParcelDeliveredEvent`; Order announces each delivered parcel (confirm and sweep), the sweep locking what it sets
- [X] T002 Order tests: one event per parcel with its own products, never twice (confirm and sweep)
- [X] T003 Identity: `given_name` claim; `ICurrentUser.GivenName` with a default
- [X] T004 Catalog: eligibility and reviews tables, rating on products, migration; consumer; handlers; controller; gateway route
- [X] T005 Catalog tests (5): eligibility, one each + average, hidden not counted, seller told once, idempotent eligibility; 2 mutation checks
- [X] T006 Bruno: reviews folder, the refusal in the seller folder (it runs last), 401 without a token
- [X] T007 Client: stars, product reviews section, card average, staff page, wording; tests
- [X] T008 Run everything; verify-saga; docs

## The same work in full (added 2026-09-27)

The list below breaks T001-T008 down to the files the merge touched. Every task is done; the original ID each
belongs to is given in brackets at the end.

### Phase 1: Foundational - the delivery announcement and the name (blocks every story)

- [X] T009 [P] Add `ParcelDeliveredEvent(OrderId, ShipmentId, BuyerId, ProductIds, DeliveredAt)` in `server/src/BuildingBlocks/Ecommerce.Contracts/Order/ParcelDeliveredEvent.cs`, product ids not variants (T001)
- [X] T010 Add `GetDeliveredParcelsAsync` and the `DeliveredParcel` record, and change the sweep's `stage` to `Func<IReadOnlyList<Guid>, CancellationToken, Task>`, in `server/src/Services/Order/Ecommerce.Order.Application/Common/Interfaces/IOrderRepository.cs` (T001)
- [X] T011 Make `SweepDeliveriesAsync` lock the due rows with `FOR UPDATE SKIP LOCKED`, set exactly those, and return their ids; implement `GetDeliveredParcelsAsync` (lines of the parcel's seller, null matching the shop's part) in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs` (T001)
- [X] T012 Add `ParcelDeliveries.AnnounceAsync` and call it from the `stage` of `ConfirmDeliveryCommandHandler` and `AutoConfirmDeliveriesCommandHandler` in `server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/ConfirmDelivery/DeliveryCommands.cs` (T001)
- [X] T013 [P] Write `A_confirmed_parcel_announces_the_products_in_it_once` and `The_sweep_announces_each_parcel_it_delivers` in `server/tests/Ecommerce.Order.Tests/DeliveryTests.cs` (T002)
- [X] T014 [P] Add the `given_name` claim (`user.FirstName`) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Security/JwtTokenGenerator.cs` (T003)
- [X] T015 [P] Add `string? GivenName => null` to `server/src/BuildingBlocks/Ecommerce.Shared/Authentication/ICurrentUser.cs` and read the claim in `CurrentUser.cs` (T003)

**Checkpoint**: a delivered parcel publishes one event naming its own products, on both paths, never twice.

### Phase 2: User Story 1 - A buyer reviews what they received (P1)

- [X] T016 [P] [US1] Add `Review` and `ReviewEligibility` in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Review.cs` (T004)
- [X] T017 [P] [US1] Add `RatingAverage` and `RatingCount` to `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Product.cs`, mapped `HasPrecision(3, 2)` and default 0 in `.../Infrastructure/Configurations/ProductConfiguration.cs` (T004)
- [X] T018 [US1] Map `product_reviews` (unique (`ProductId`, `CustomerId`), index (`ProductId`, `CreatedAt`), CHECK `CK_product_reviews_rating`, cascade FK) and `review_eligibility` (composite key) in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/ReviewConfiguration.cs`; add both `DbSet`s to `.../Persistence/CatalogDbContext.cs` (T004)
- [X] T019 [US1] Generate `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260923212320_AddProductReviews.cs` - additive only (T004)
- [X] T020 [US1] Declare `IReviewRepository` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IReviewRepository.cs` and implement it in `.../Infrastructure/Persistence/Repositories/ReviewRepository.cs`: `RecordEligibilityAsync` with `ON CONFLICT DO NOTHING`, `SaveAndRecomputeAsync` saving and recomputing from visible rows in one transaction; register it in `.../Infrastructure/DependencyInjection.cs` (T004)
- [X] T021 [US1] Add `RecordReviewEligibilityCommand`, `GetMyReviewQuery`, `WriteReviewCommand` with its validator, and their handlers in `ReviewHandlers` - 403 `NotEligible` in words, author name from `given_name` or the email's initial, `ReviewPosted` / `ReviewEdited` audit - in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs` (T004)
- [X] T022 [US1] Add `ReviewEligibilityConsumer` in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Consumers/ReviewEligibilityConsumer.cs` and register it in `.../WebApi/Program.cs` (T004)
- [X] T023 [US1] Add `ReviewsController` with `GET` and `PUT products/{productId}/reviews/mine` in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/ReviewsController.cs` (T004)
- [X] T024 [P] [US1] Write `Somebody_who_has_not_received_it_cannot_review_it`, `One_review_each_signed_with_the_first_name_and_the_average_follows` and `Receiving_it_twice_is_one_right_to_review` in `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs`; give `TestCaller` settable `Email` and `GivenName` and register `IReviewRepository` in `CatalogTestFixture.cs` (T005)

**Checkpoint**: quickstart scenarios 1-3 pass.

### Phase 3: User Story 2 - Shoppers see what buyers thought (P1)

- [X] T025 [US2] Add `GetProductReviewsQuery` (visible, newest first, paging validator 1-50) to `.../Application/Reviews/ReviewFeatures.cs` and the anonymous `GET products/{productId}/reviews` to `ReviewsController.cs` (T004)
- [X] T026 [P] [US2] Add `ratingAverage` and `ratingCount` to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Common/ProductResponse.cs` (T004)
- [X] T027 [P] [US2] Add `StarRating` and `StarInput` in `client/src/components/product/star-rating/index.tsx` (T007)
- [X] T028 [US2] Add `Reviews` in `client/src/services/review/index.ts` and `types.ts`, the hooks in `client/src/hooks/review/index.ts`, query keys in `client/src/constants/query-keys/index.ts`, and `ratingAverage` / `ratingCount` in `client/src/services/product/types.ts` (T007)
- [X] T029 [US2] Add `ProductReviews` (average, list, form only when eligible, refusal in the server's words) in `client/src/components/product/product-reviews/index.tsx`, used by `client/src/pages/product/index.tsx`; the average on `client/src/components/product/product-card/index.tsx`; wording in `client/src/locales/{en,vi}/catalog.json` (T007)
- [X] T030 [P] [US2] Write `client/src/components/product/product-reviews/index.test.tsx` (five tests) and update the product fixtures in the existing page tests for the two new fields (T007)

**Checkpoint**: quickstart scenario 4 passes.

### Phase 4: User Story 3 - Moderators hide what does not belong (P2)

- [X] T031 [US3] Add `GetReviewsForStaffQuery`, `HideReviewCommand` (reason required, at most 500) and `RestoreReviewCommand`, 409 on the wrong state, `ReviewHidden` / `ReviewRestored` under `Moderation`, in `.../Application/Reviews/ReviewFeatures.cs`; `GetForStaffAsync` with the product name in `ReviewRepository.cs` (T004)
- [X] T032 [US3] Add `GET reviews`, `POST reviews/{id}/hide` and `/restore` (`StaffRoles.Staff`) to `ReviewsController.cs` (T004)
- [X] T033 [US3] Add `catalog-reviews-route` and `catalog-reviews-root-route` to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json` (T004)
- [X] T034 [P] [US3] Write `A_hidden_review_is_neither_shown_nor_counted` in `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs` (T005)
- [X] T035 [US3] Add `client/src/pages/admin-reviews/index.tsx` (Shown / Hidden tabs, hide asks for a reason, restore), its route in `client/src/routes/index.tsx`, its link in `client/src/layouts/admin-layout/index.tsx`, wording in `client/src/locales/{en,vi}/admin.json` (T007)
- [X] T036 [P] [US3] Write `client/src/pages/admin-reviews/index.test.tsx` (two tests) (T007)

**Checkpoint**: quickstart scenario 5 passes.

### Phase 5: User Story 4 - The seller hears about it (P3)

- [X] T037 [US4] Add `NotificationKind.NewReview` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs` and send it from the new-review branch only of `WriteReviewCommand`'s handler (T004)
- [X] T038 [P] [US4] Write `The_seller_is_told_about_a_new_review_not_about_every_edit` in `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs` (T005)
- [X] T039 [P] [US4] Word `NewReview` in `client/src/locales/en/notifications.json` and `client/src/locales/vi/notifications.json` (T007)

**Checkpoint**: quickstart scenario 6 passes.

### Phase 6: Polish and evidence

- [X] T040 [P] Add `bruno/reviews/` (folder seq 14, ten requests from "the customer may review what arrived" to "a moderator restores it") (T006)
- [X] T041 [P] Add `bruno/seller/someone who did not receive it cannot review it.yml` (403 with the sentence; in the seller folder because it runs last and sets the seller's token) and `bruno/security-checks/reviewing without a token is 401.yml` (T006)
- [X] T042 Run the mutation checks - count hidden reviews in the recompute; skip the eligibility check - each red, then restored (T005)
- [X] T043 Update `CLAUDE.md` with the reviews paragraph (T008)
- [X] T044 Run Order (176/176), Catalog (149/149), Identity (71/71), the client (221/221, lint, type-check, build), Bruno (180/180 requests, 290 tests) and `verify-saga.sh`; screenshots of the reviews section at 1360 px and 390 px with no overflow (T008)
- [X] T045 Merge as PR #98, `feat(catalog): buyers rate and review what they received`, closing #91 (merge commit `c8a64d5`)

## Dependencies & Execution Order

- **Phase 1** blocks US1: without the event nobody is ever eligible.
- **US1** before US2 and US3: they read and moderate what US1 writes. US2's server read is independent of US1's
  write path, but its tests need reviews to exist.
- **US4** is a branch inside US1's write handler.
- T033 (the gateway) is needed by every Bruno request under `/api/reviews`.

## Implementation notes

- **Documentation outside `CLAUDE.md`** (`docs/features/ratings-and-reviews.md`, the timeline row, the decision
  log rows 28 and 29) is not in the PR's file list; it arrived the same day in `f466a63`, "docs: a documentation
  set fit for the project report".
- **One file for all review use cases.** The requests, validators and handlers live in
  `Application/Reviews/ReviewFeatures.cs` rather than a folder per use case (plan, Constitution Check II).
- **Bruno debris.** The PR notes that test products had built up to 57 and pushed `search without diacritics` off
  its first page; they were purged with `local/purge-products.sh`. Not part of this change.

## Notes

- 45 tasks: the 8 original, and 37 that break them down (7 foundational, 9 for US1, 6 for US2, 6 for US3, 3 for
  US4, 6 polish).
- Test tasks: T013, T024, T030, T034, T036, T038 - seven server tests and seven client tests.
