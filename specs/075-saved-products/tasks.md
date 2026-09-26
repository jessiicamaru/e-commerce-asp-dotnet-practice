---
description: "Task list for saved products"
---

# Tasks: A shopper saves a product for later

> Completed on 2026-09-27, after the feature merged (#159), from the code at that merge, the pull request and docs/features/saved-products.md.

**Input**: Design documents from `/specs/075-saved-products/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The idempotency of saving is the database's guarantee (Constitution III and V), so it is tested
against a real PostgreSQL; the once-per-flip rule is tested by sending availability announcements through the real
handler and reading what the harness published.

**Organization**: T001 to T005 are the tasks as written when the feature was built, kept verbatim. T006 onwards
break the same work down to file level, in dependency order, grouped by user story. Every task is done.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US3)

---

## The tasks as written

- [X] T001 Tests first, in `SavedProductTests`:
  - save is idempotent under concurrency;
  - unsaving something unsaved is fine;
  - a hidden product cannot be saved (404);
  - the list is newest first and holds only the caller's;
  - a product taken down reads as unavailable;
  - a deleted product leaves the list;
  - the ids.
- [X] T002 The entity, configuration, migration, repository, features and controller.
- [X] T003 Back in stock: the rollup reports the flip, the handler notifies the savers, and the kind is declared. Tests: one notice per flip, none for an unlisted product, none on a repeat.
- [X] T004 Storefront: the service, hooks, heart, page, route, menu and words. Vitest.
- [X] T005 Bruno, the reference, mutation checks, and the docs.

---

## Phase 1: Setup

- [X] T006 Register `ISavedProductRepository` in the test fixture `server/tests/Ecommerce.Catalog.Tests/CatalogTestFixture.cs`, so the tests can be written first

## Phase 2: Foundational (blocks every story)

- [X] T007 [P] Create the entity `SavedProduct` (`CustomerId`, `ProductId`, `SavedAt`, `Product`) in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/SavedProduct.cs`
- [X] T008 [P] Map it in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/SavedProductConfiguration.cs`: table `saved_products`, key `(CustomerId, ProductId)`, indexes `(CustomerId, SavedAt)` and `ProductId`, cascade from `products`
- [X] T009 Add `DbSet<SavedProduct> SavedProducts` to `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/CatalogDbContext.cs`
- [X] T010 Generate the migration `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260926095302_AddSavedProducts.cs` (with its Designer and the model snapshot) - add-only, so an earlier image runs against it
- [X] T011 Declare `ISavedProductRepository` (`SaveAsync`, `UnsaveAsync`, `GetPageAsync`, `IdsAsync`, `SaverIdsAsync`) in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/ISavedProductRepository.cs`
- [X] T012 Implement it in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/SavedProductRepository.cs`: `INSERT ... ON CONFLICT ("CustomerId", "ProductId") DO NOTHING`, a guarded `ExecuteDeleteAsync`, and a page with the listing's includes (category, translations, variants and their prices) as a split query
- [X] T013 Register the repository in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/DependencyInjection.cs`

## Phase 3: User Story 1 - Save and unsave (P1)

- [X] T014 [US1] Write `Saving_twice_or_twenty_times_at_once_keeps_one_entry`, `Unsaving_removes_it_and_unsaving_what_was_never_saved_is_no_error` and `Only_a_product_on_sale_can_be_saved` in `server/tests/Ecommerce.Catalog.Tests/SavedProductTests.cs`
- [X] T015 [US1] Add `SaveProductCommand`, `UnsaveProductCommand`, `GetSavedProductIdsQuery` and their handlers to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Saved/SavedProductFeatures.cs` - the caller from `ICurrentUser`, and a product not listed or not active is `NotFoundException("Product not found.")`
- [X] T016 [US1] Add `PUT` and `DELETE /api/products/{id:guid}/saved` and `GET /api/products/saved/ids` to `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/SavedProductsController.cs`, `[Authorize]` on the class
- [X] T017 [P] [US1] Add the `SavedProduct` service class (`save`, `unsave`, `ids`) in `client/src/services/saved-product/index.ts`, with its test `client/src/services/saved-product/index.test.ts` asserting the URLs name no shopper
- [X] T018 [US1] Add `useSavedIds` (disabled while signed out, one request shared by every heart) and `useToggleSaved` (invalidates `['saved']`) in `client/src/hooks/saved-product/index.ts`, with `savedIds` and `savedProducts` keys in `client/src/constants/query-keys/index.ts`
- [X] T019 [US1] Build the heart `client/src/components/product/save-button/index.tsx` (`aria-pressed`, a signed-out tap goes to `/sign-in` with `state.from`), and its test `client/src/components/product/save-button/index.test.tsx`; add `renderSignedOut` to `client/src/test/render.tsx`
- [X] T020 [US1] Put the heart on the card, beside the link rather than inside it, in `client/src/components/product/product-card/index.tsx`, and beside the title in `client/src/pages/product/index.tsx`
- [X] T021 [P] [US1] Add `saved.save`, `saved.unsave` to `client/src/locales/vi/catalog.json` and `client/src/locales/en/catalog.json`

**Checkpoint**: a shopper saves and unsaves from any card; the ids hold each product once.

## Phase 4: User Story 2 - The saved list (P1)

- [X] T022 [US2] Write `The_list_is_the_callers_own_newest_first_in_the_listings_words` and `A_product_taken_down_since_stays_and_reads_as_unavailable_and_a_deleted_one_goes` in `server/tests/Ecommerce.Catalog.Tests/SavedProductTests.cs`
- [X] T023 [US2] Add `GetSavedProductsQuery`, its validator (page >= 1, page size 1-50) and `SavedProductResponse(Product, SavedAt, Available)` to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Saved/SavedProductFeatures.cs`, mapping each product with `ProductResponse.From` in the request's language and currency and the shop name from `ISellerRepository`
- [X] T024 [US2] Add `GET /api/products/saved?page=&pageSize=` to `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/SavedProductsController.cs`
- [X] T025 [US2] Add `list` and the `SavedItem` / `SavedPage` types in `client/src/services/saved-product/index.ts` and `client/src/services/saved-product/types.ts`, and `useSavedProducts` in `client/src/hooks/saved-product/index.ts`
- [X] T026 [US2] Build `client/src/pages/saved/index.tsx` (the cards, `PAGE_SIZE` per page with `Pager`, an unavailable one dimmed and labelled, an empty state) and its test `client/src/pages/saved/index.test.tsx`
- [X] T027 [US2] Route `/saved` behind `RequireAuth` in `client/src/routes/index.tsx`, and link **Saved** from `client/src/components/layout/user-menu/index.tsx`
- [X] T028 [P] [US2] Add the rest of `saved.*` to `client/src/locales/{vi,en}/catalog.json` and `nav.saved` to `client/src/locales/{vi,en}/common.json`

**Checkpoint**: `/saved` lists the caller's own products, newest first; one taken down reads as unavailable.

## Phase 5: User Story 3 - Back in stock (P2)

- [X] T029 [US3] Write `Coming_back_in_stock_tells_whoever_saved_it_once_per_flip` (two savers; in, still in, out, in; four notices) and `A_product_off_the_shelf_coming_back_in_stock_tells_nobody` in `server/tests/Ecommerce.Catalog.Tests/SavedProductTests.cs`
- [X] T030 [US3] Change `RecomputeProductRollupAsync` to `Task<bool>` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs`, and in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs` read the value the statement started from in a CTE and return it with `RETURNING`
- [X] T031 [US3] In `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Availability/RecordStockAvailabilityCommandHandler.cs`, on the flip and only for a listed, active product, publish `SavedBackInStock` with `{ product }` and the link `/products/{id}` to each saver through `INotifier`
- [X] T032 [P] [US3] Add `NotificationKind.SavedBackInStock` to `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs` and declare `"SavedBackInStock": { "required": ["product"] }` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json`
- [X] T033 [P] [US3] Word `kind.SavedBackInStock` in `client/src/locales/vi/notifications.json` and `client/src/locales/en/notifications.json`

**Checkpoint**: stocking a saved product tells each saver once.

## Phase 6: Polish and cross-cutting

- [X] T034 [P] Add `bruno/product/` requests 61-66: save (204), save again (204), the ids hold it once, the list's shape, unsave (204), and 401 without a token
- [X] T035 Run the mutation checks - remove `ON CONFLICT`; let an unlisted product be saved; ignore "was" in the flip; notify for a product off the shelf - and confirm each turns `SavedProductTests` red
- [X] T036 Run the suites: `Ecommerce.Catalog.Tests` 176/176, the storefront 407/407 with lint and `tsc` clean, Bruno 238/238 requests and 387/387 tests through the storefront container; and the live check through the gateway (a saved product stocked by an administrator produced a `SavedBackInStock` notice)
- [X] T037 [P] Write `docs/features/saved-products.md`, and update `docs/README.md`, `docs/overview/project-overview.md`, `docs/project/backlog.md` (#109 to Fixed), `docs/project/timeline.md`, `docs/testing/testing-strategy.md` and `CLAUDE.md`
- [X] T038 Regenerate the reference with `python docs/tools/generate_reference.py` (`docs/reference/api.md`: 4 endpoints; `docs/reference/data-model.md`: 1 table)
- [X] T039 Merge through PR #159, "feat(catalog): a shopper saves a product for later, and hears when it is back in stock", closing #109 (merged 2026-09-26)

## Dependencies

- T006-T013 before any story. T014, T022 and T029 (tests) before the code that makes them pass.
- US1 before US2 in the storefront (the page reuses the service and hooks); on the server they are independent.
- US3 depends only on the foundational repository (`SaverIdsAsync`) and on US1 for there to be savers.
- T035-T036 after all stories; T039 last.

## Notes

- 39 tasks: the 5 as originally written, then 34 at file level (1 setup, 7 foundational, 8 for US1, 7 for US2,
  5 for US3, 6 polish).
- Test tasks: T014, T022 and T029 (7 server tests) and the three Vitest files in T017, T019 and T026.
- The mutation checks and the counts in T035-T036 are from PR #159. Whether the tests were in fact written before
  the code, as T001 says, is not recorded beyond that task's wording.
