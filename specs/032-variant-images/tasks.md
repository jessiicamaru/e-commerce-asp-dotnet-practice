# Tasks: The picture follows the variant

## Phase 1: The column and the key

- [X] T001 `ImageContentType` and `ImageUpdatedAt` on ProductVariant in server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/ProductVariant.cs
- [X] T002 EF configuration with the same CHECK constraint the product has
- [X] T003 Migration, additive only
- [X] T004 `ProductImageKey.ForVariant` with the `variant-` prefix; the product form unchanged
- [X] T005 Test: a product and its first variant produce different keys for the same instant

## Phase 2: The writes

- [X] T006 `UploadVariantImageCommand` and `RemoveVariantImageCommand`, modelled on the product ones
- [X] T007 `TrySetVariantImageAsync` guarded update on the repository
- [X] T008 Three routes on ProductsController, `"Seller,Admin"` on the two writes
- [X] T009 Tests: owner uploads, another seller 404, replace leaves one file, SVG refused, over 2MB refused

## Phase 3: The read

- [X] T010 `VariantResponse.ImageUrl`, resolved to the variant's or the product's
- [X] T011 Tests: a variant with its own, a variant falling back, neither

## Phase 4: The cleanup

- [X] T012 Test seen RED: deleting a product leaves no variant image
- [X] T013 `DeleteProductCommandHandler` collects every variant key beside the product's
- [X] T014 Run the Catalog suite

## Phase 5: The storefront

- [X] T015 The product page shows the chosen variant's picture
- [X] T016 Seller upload per variant on client/src/pages/shop-product/index.tsx
- [X] T017 Strings in both locales
- [X] T018 Vitest tests: the picture follows the chooser, the fallback, what the upload sends

## Phase 6: End to end

- [X] T019 Two real sellers: SC-002, 404 never 403
- [X] T020 A real upload per variant, and a screenshot of the chooser swapping
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
