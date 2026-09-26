---
description: "Task list for Product review before sale"
---

# Tasks: Product review before sale

> Completed on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Input**: Design documents from `/specs/045-product-review/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. A decision that must happen once and a filter that must cover checkout are
exactly what constitution Principle V says cannot be checked by hand.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 hidden until approved, US2 moderators decide, US3 edits send back, US4 dashboard

---

## As recorded at merge

The nine tasks the feature was built from, kept as written. The phases below break each into the files
it touched; every task there names the original it belongs to.

- [X] T001 Catalog: review columns (default Approved), index, migration; `IsListed`, `Sellable` requires it
- [X] T002 Catalog: listing, lookup and pricing hide what is not approved; a seller's own list does not
- [X] T003 Catalog: create starts a seller's product Pending; queue, approve, reject, take down, resubmit with a guarded update and a stage
- [X] T004 Catalog: seller edits to text or images send an approved product back (six handlers)
- [X] T005 Tests: `ProductReviewTests` (7); mutation checks on the shelf filter, the sellable gate, and edit-sends-back
- [X] T006 Activity: `GET /api/audit/mine` for staff
- [X] T007 Bruno: the seller folder follows a product through pending, approved, renamed, rejected and resubmitted; 403s
- [X] T008 Client: review queue, moderator dashboard, seller badge and banner, wording; tests
- [X] T009 Run everything; verify-saga; docs; local demo seeder approves its sellers' products

> Note added on 2026-09-27: "docs" in T009 was `CLAUDE.md` - the pull request changed no file under
> `docs/`; the feature pages there were written later (`f466a63`). The demo seeder it mentions was
> local and gitignored, so it is not in the repository.

---

## Phase 1: Foundational - the review state (part of T001)

- [X] T010 Add `ProductReviewStatus` (`Approved`, `Pending`, `Rejected`) and `ReviewStatus`, `ReviewReason`, `SubmittedAt`, `ReviewedAt`, `ReviewedBy`, `IsListed` to `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Product.cs`
- [X] T011 Map them in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs`: string conversion, max 20, required; reason max 500; `Ignore(IsListed)`; index on (`ReviewStatus`, `SubmittedAt`)
- [X] T012 Generate `20260923210256_AddProductReview` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/` and set `defaultValue: "Approved"` on `ReviewStatus`, with the comment saying why (research D1)
- [X] T013 [P] Add `ReviewStatus` and `ReviewReason` to `ProductResponse` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Common/ProductResponse.cs`, additive with defaults
- [X] T014 [P] Add `ProductApproved`, `ProductRejected`, `ProductTakenDown` to `NotificationKind` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs`
- [X] T015 [P] Register `AddNotifier()` in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs` and in `server/tests/Ecommerce.Catalog.Tests/CatalogTestFixture.cs`

**Checkpoint**: the migration applies; every existing product reads `Approved`.

---

## Phase 2: User Story 1 - Nothing a seller lists is on sale until a moderator looks (P1) (T001, T002, part of T003)

- [X] T016 [US1] Create `ProductReview` with `StartsPending` and `MaySee` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/ProductReview.cs`
- [X] T017 [US1] Start a seller's product `Pending` with `SubmittedAt`, an administrator's `Approved`, in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Commands/CreateProduct/CreateProductCommandHandler.cs`
- [X] T018 [US1] Add `listedOnly = true` to `GetPaginatedAsync` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs` and filter on `Approved` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs`
- [X] T019 [P] [US1] Pass `listedOnly: false` from `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Queries/GetMyProducts/GetMyProductsQuery.cs`
- [X] T020 [P] [US1] Return null (404) unless `ProductReview.MaySee` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Queries/GetProductById/GetProductByIdQueryHandler.cs`
- [X] T021 [P] [US1] Make `ProductVariant.Sellable` require `Product.IsListed` in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/ProductVariant.cs`
- [X] T022 [P] [US1] Answer `Sellable = IsActive && IsListed` in `GetPrices` and `DescribeProducts` in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Grpc/CatalogPricingService.cs`
- [X] T023 [US1] Test `A_sellers_new_product_is_hidden_and_unsellable_until_approved` and `The_shops_own_product_is_on_sale_as_listed` in `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs`

**Checkpoint**: a seller's product is 404, absent and unsellable to a shopper; visible to its seller and staff.

---

## Phase 3: User Story 2 - Moderators decide (P1) (T003)

- [X] T024 [US2] Declare `GetForReviewAsync` and `TryReviewAsync(productId, from, to, reason, reviewedBy, at, stage)` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs`
- [X] T025 [US2] Implement them in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs`: the queue order (pending oldest first, history newest first), and the guarded `ExecuteUpdateAsync ... WHERE "ReviewStatus" IN (from)` inside the execution strategy and one transaction, running `stage` and the one save only when a row moved
- [X] T026 [US2] Write `GetReviewQueueQuery`, `ApproveProductCommand`, `RejectProductCommand`, `TakeDownProductCommand`, `ResubmitProductCommand`, the two reason validators and `ProductReviewHandlers` (audit entry and notice in the stage; 409 when nothing moved; resubmit through `SellerOwnership`) in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Review/ProductReviewFeatures.cs`
- [X] T027 [US2] Add `GET review`, `POST {id}/approve`, `/reject`, `/take-down` (`StaffRoles.Staff`) and `POST {id}/resubmit` (`Seller,Admin`) to `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs`
- [X] T028 [US2] Test approval once with its notice and audit entry, rejection then resubmit, take-down, and the queue order in `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs` (part of T005)

**Checkpoint**: staff decide once; the seller is told; a second decision is 409.

---

## Phase 4: User Story 3 - Changing what a shopper sees goes back to review (P1) (T004)

- [X] T029 [US3] Add `AfterSellerEditAsync` (approved, has a seller, caller not Admin → `Pending`, `SubmittedAt`, `ProductSentForReview` audit entry) to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/ProductReview.cs`
- [X] T030 [P] [US3] Call it before the one save in the set and remove translation handlers in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Translations/SetProductTranslationCommand.cs` (the remove handler gains `IAuditTrail`)
- [X] T031 [P] [US3] Call it in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Images/UploadProductImage/UploadProductImageCommand.cs` and `.../RemoveProductImage/RemoveProductImageCommand.cs`
- [X] T032 [P] [US3] Call it in both handlers of `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Images/VariantImageCommands.cs` (research D4)
- [X] T033 [US3] Test `Editing_the_name_sends_an_approved_product_back_and_a_price_does_not` in `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs` (part of T005)

---

## Phase 5: User Story 4 - A moderator's dashboard (P2) (T006, part of T008)

- [X] T034 [US4] Add `MyDecisionsController` (`GET /api/audit/mine`, Staff, `GetAuditEntriesQuery(Category: "Moderation", ActorId: <token>)`) in `server/src/Services/Activity/Ecommerce.Activity.WebApi/Controllers/MyDecisionsController.cs`
- [X] T035 [P] [US4] Write `Moderation` (queue, approve, reject, take down, my decisions) in `client/src/services/moderation/index.ts` and `useReviewQueue`, `useMyDecisions`, `useReviewDecision` in `client/src/hooks/moderation/index.ts`; query keys in `client/src/constants/query-keys/index.ts`
- [X] T036 [US4] Build the dashboard in `client/src/pages/admin-moderation/index.tsx` (counts from each queue's `totalCount`, `RECENT_DECISIONS = 8`)
- [X] T037 [US2] Build the review queue in `client/src/pages/admin-products/index.tsx`: tabs Pending/Rejected/Approved, approve, reject and take down through a reason dialog, `PAGE_SIZE`, the server's 409 shown in its words
- [X] T038 [P] [US4] Add the two routes in `client/src/routes/index.tsx`, the Moderation and Review menu entries in `client/src/layouts/admin-layout/index.tsx`, and send a moderator to `/admin/moderation` from `client/src/pages/admin-home/index.tsx`

---

## Phase 6: The seller's side in the storefront (US2, US3; part of T008)

- [X] T039 [P] [US2] Add `reviewStatus`, `reviewReason` and `ReviewStatus` to `client/src/services/product/types.ts`, `Product.resubmit` to `client/src/services/product/index.ts`, `useResubmitProduct` to `client/src/hooks/product/index.ts`
- [X] T040 [P] [US1] Add `ReviewBadge` in `client/src/components/product/review-badge/index.tsx` and show it in `client/src/pages/shop-products/index.tsx` and `client/src/pages/shop-product/index.tsx`
- [X] T041 [US2] Add `ReviewBanner` (pending, rejected with reason and resubmit, approved hint) in `client/src/components/seller/review-banner/index.tsx` and show it in `client/src/pages/shop-product/index.tsx`
- [X] T042 [P] Word everything in `client/src/locales/{en,vi}/admin.json`, `seller.json` and `notifications.json` (the three new kinds)
- [X] T043 Test `client/src/pages/admin-products/index.test.tsx` (5), `client/src/pages/admin-moderation/index.test.tsx` (1), `client/src/components/seller/review-banner/index.test.tsx` (3); update the admin-layout, shop, shop-products, shop-product, product and cart tests for the new fields and menu

---

## Phase 7: End to end and polish (T005, T007, T009)

- [X] T044 [P] Add to `bruno/seller/`: `the new product is not on the shelf yet`, `its seller sees it waiting`, `a seller cannot approve their own product`, `staff see it in the review queue`, `an administrator approves the product`, `staff see what they decided`, `renaming it sends it back to review`, `the renamed product is off the shelf`, `a moderator takes it down instead` (a rejection), `the seller sends it back`; renumber the folder's later requests
- [X] T045 [P] Add `bruno/security-checks/a customer cannot read the review queue.yml` and `a customer has no decisions to read.yml` (403 each)
- [X] T046 Run the three mutation checks - listing filter, `Sellable` ignoring review, the translation handler's edit hook - each red, then restored
- [X] T047 Run Catalog (144/144), Identity (71/71) and client (214/214) tests, lint, type-check and build, the Bruno collection (168 requests, 268 tests) and `verify-saga.sh`; screenshot the dashboard and queue at 1360px and the queue at 390px
- [X] T048 Describe the feature and the Catalog test count in `CLAUDE.md`
- [X] T049 Merge through PR #97 (`feat(catalog): a seller's product waits for a moderator before it goes on sale`, closes #90), 2026-09-23 21:24 UTC

---

## Dependencies & Execution Order

- Phase 1 blocks everything: nothing can ask `IsListed` before the column exists.
- US1 (Phase 2) before US2 (Phase 3): a queue of products nobody can hide is pointless, and US2's tests
  assert that approval makes a hidden product visible.
- US3 (Phase 4) needs only Phase 1 and `ProductReview`; it can run beside US2.
- US4 and the storefront (Phases 5-6) need the endpoints from Phase 3.
- Phase 7 last.

## Notes

- 49 tasks: the 9 recorded at merge, and 40 that break them into files.
- Later work on this area, not part of this feature: specs/056 (#126) hooked two more edits; specs/081
  (#166) hid a hidden product's images, reviews and questions; #169 added a Bruno request that really
  takes a product down.
