---
description: "Task list for The picture follows the variant"
---

# Tasks: The picture follows the variant

> Completed on 2026-09-27, after the feature merged (#73), from the code at that merge, the pull request and
> docs/features/catalog.md. Story labels and paths were added to the existing tasks; T028 onward were
> added in this backfill.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

**Tests**: included - an ownership boundary and a leak that is invisible until someone lists the store.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel; **[Story]**: US1 picture follows choice, US2 seller photographs,
  US3 nothing left behind, US4 card unchanged

## Phase 1: The column and the key

- [X] T001 [US2] `ImageContentType` and `ImageUpdatedAt` on ProductVariant in server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/ProductVariant.cs
- [X] T002 [US2] EF configuration with the same CHECK constraint the product has
- [X] T003 [US2] Migration, additive only (server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260923055302_AddVariantImages.cs)
- [X] T004 [US2] `ProductImageKey.ForVariant` with the `variant-` prefix; the product form unchanged
- [X] T005 [P] [US2] Test: a product and its first variant produce different keys for the same instant

## Phase 2: The writes

- [X] T006 [US2] `UploadVariantImageCommand` and `RemoveVariantImageCommand`, modelled on the product ones
- [X] T007 [US2] `TrySetVariantImageAsync` guarded update on the repository
- [X] T008 [US2] Three routes on ProductsController, `"Seller,Admin"` on the two writes
- [X] T009 [US2] Tests: owner uploads, another seller 404, replace leaves one file, SVG refused, over 2MB refused

## Phase 3: The read

- [X] T010 [US1] `VariantResponse.ImageUrl`, resolved to the variant's or the product's
- [X] T011 [US1] Tests: a variant with its own, a variant falling back, neither

## Phase 4: The cleanup

- [X] T012 [US3] Test seen RED: deleting a product leaves no variant image
- [X] T013 [US3] `DeleteProductCommandHandler` collects every variant key beside the product's
- [X] T014 Run the Catalog suite

## Phase 5: The storefront

- [X] T015 [US1] The product page shows the chosen variant's picture
- [X] T016 [US2] Seller upload per variant on client/src/pages/shop-product/index.tsx
- [X] T017 [P] [US2] Strings in both locales
- [X] T018 [US1] [US2] Vitest tests: the picture follows the chooser, the fallback, what the upload sends

## Phase 6: End to end

- [X] T019 [US2] Two real sellers: SC-002, 404 never 403
- [X] T020 [US1] A real upload per variant, and a screenshot of the chooser swapping
- [X] T021 `verify-saga.sh`, Bruno

## Phase 7: Say so

- [X] T022 CLAUDE.md, including the specs/019 paragraph that says images hang on products
- [X] T023 PR closing #72

## Added while building

- [X] T024 `FileSystemProductImageStore` validates the key shape before it becomes a path and
      refused the `variant-` prefix. Found by running the test, not by reading the code. The pattern
      is widened by one optional group and still admits no slash, no dot segment, no traversal
- [X] T025 The refusal uses `SellerOwnership.CanWrite`, not `RequireCanWrite`: that one throws a
      message naming the PRODUCT id, which would have made "not yours" distinguishable from "no such
      variant" - the exact thing the 404 exists to prevent
- [X] T026 Three test setups were wrong, not the code: `SeedProductAsync` creates no variant at all,
      and `ProductResponse.From` leaves `Variants` null - only the lookup fills it
- [X] T027 The screenshot script clicked the radio, which is visually hidden; the label is the click
      target, which is the chooser's whole design

## Added in the 2026-09-27 backfill (work the pull request shows)

- [X] T028 [US2] `GetVariantImageQuery` in server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Images/GetVariantImageQuery.cs - 404 when the variant has no photograph of its own or names a different product; logs an error when the row names a file the store lacks
- [X] T029 [US2] `VariantImages.RequireOwnedVariantAsync` in VariantImageCommands.cs - the one 404 (`Variant with ID '…' was not found.`) for missing, wrong product, and another seller's
- [X] T030 [US2] A concurrent replacement loses with 409 and its new file is deleted (the guarded `TrySetVariantImageAsync` affecting zero rows)
- [X] T031 [P] [US1] `ProductImage` component and `useUploadVariantImage` / `useRemoveVariantImage` hooks in client/src/components/product/product-image/index.tsx and client/src/hooks/product/index.ts
- [X] T032 [US1] Bruno `seller/a seller photographs one shape.yml` - every variant carries an image address when the product has one, none when it does not
- [X] T033 The design record completed to the specs/001 standard: data-model.md, quickstart.md, plan structure, research labels and the D3 correction (2026-09-27)
- [X] T034 Merged as **#73** (`35ad95f`) on 2026-09-23, closing #72: Catalog 122, client 43 tests; Bruno 94/94 requests, 147/147 tests; `verify-saga.sh` green
