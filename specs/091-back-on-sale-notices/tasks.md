---
description: "Task list for A saved product back on sale by any route tells whoever saved it"
---

# Tasks: A saved product back on sale by any route tells whoever saved it

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - two failed before the fix.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Survey

- [X] T001 Every caller of `RecomputeProductRollupAsync`, every writer of `ReviewStatus = Approved` and of `Product.IsActive` (research D3)

## Phase 2: Tests first

- [X] T002 [US1] `Reactivating_a_variant_in_stock_tells_whoever_saved_it` in `server/tests/Ecommerce.Catalog.Tests/SavedProductTests.cs`
- [X] T003 [US2] `Approving_a_product_in_stock_that_was_off_the_shelf_tells_whoever_saved_it`, `Approving_a_product_with_nothing_in_stock_tells_nobody`
- [X] T004 [US3] `An_edit_that_leaves_it_on_sale_tells_nobody_again` (variant edit, price, new variant)
- [X] T005 Run before the fix: T002 and the first of T003 red; the other two green

## Phase 3: Implementation

- [X] T006 `SavedProductNotices` (new) in `.../Products/Saved/`; `RecordStockAvailabilityCommandHandler` uses it
- [X] T007 [US1] [US3] `SaveAndRecomputeRollupAsync` in `IProductRepository` / `ProductRepository`; `UpdateProductVariant`, `AddProductVariant`, `SetVariantPrice` call it
- [X] T008 [US2] Approval tells in `ProductReviewFeatures.MoveAsync`'s `stage`
- [X] T009 [P] [US4] "available again" in `client/src/locales/{en,vi}/{notifications,admin}.json` and `EmailTemplates.cs`

## Phase 4: Verification and docs

- [X] T010 Mutations (quickstart Scenario 4) - each red; `Ecommerce.Catalog.Tests` 215/215; storefront suite green
- [X] T011 Docs: `docs/features/saved-products.md`, `docs/features/email.md`, `docs/project/backlog.md`, `docs/project/timeline.md`, CLAUDE.md
- [ ] T012 Merged as #190, closing #182

## Verification

- Before: `Reactivating_a_variant_in_stock_tells_whoever_saved_it` and
  `Approving_a_product_in_stock_that_was_off_the_shelf_tells_whoever_saved_it` red (2 of 11 in `SavedProductTests`).
- After: `Ecommerce.Catalog.Tests` 215/215; `npx vitest run` green.
- Mutations, each restored: approval never tells - the approval test red; approval ignores stock - the no-stock test
  red; callback without a flip - the reactivation and "leaves it on sale" tests red; callback never runs - the
  reactivation test red.
