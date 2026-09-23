# Tasks: The picture follows the variant

## Phase 1: The column and the key

- [ ] T001 `ImageContentType` and `ImageUpdatedAt` on ProductVariant in server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/ProductVariant.cs
- [ ] T002 EF configuration with the same CHECK constraint the product has
- [ ] T003 Migration, additive only
- [ ] T004 `ProductImageKey.ForVariant` with the `variant-` prefix; the product form unchanged
- [ ] T005 Test: a product and its first variant produce different keys for the same instant

## Phase 2: The writes

- [ ] T006 `UploadVariantImageCommand` and `RemoveVariantImageCommand`, modelled on the product ones
- [ ] T007 `TrySetVariantImageAsync` guarded update on the repository
- [ ] T008 Three routes on ProductsController, `"Seller,Admin"` on the two writes
- [ ] T009 Tests: owner uploads, another seller 404, replace leaves one file, SVG refused, over 2MB refused

## Phase 3: The read

- [ ] T010 `VariantResponse.ImageUrl`, resolved to the variant's or the product's
- [ ] T011 Tests: a variant with its own, a variant falling back, neither

## Phase 4: The cleanup

- [ ] T012 Test seen RED: deleting a product leaves no variant image
- [ ] T013 `DeleteProductCommandHandler` collects every variant key beside the product's
- [ ] T014 Run the Catalog suite

## Phase 5: The storefront

- [ ] T015 The product page shows the chosen variant's picture
- [ ] T016 Seller upload per variant on client/src/pages/shop-product/index.tsx
- [ ] T017 Strings in both locales
- [ ] T018 Vitest tests: the picture follows the chooser, the fallback, what the upload sends

## Phase 6: End to end

- [ ] T019 Two real sellers: SC-002, 404 never 403
- [ ] T020 A real upload per variant, and a screenshot of the chooser swapping
- [ ] T021 `verify-saga.sh`, Bruno

## Phase 7: Say so

- [ ] T022 CLAUDE.md, including the specs/019 paragraph that says images hang on products
- [ ] T023 PR closing #72
