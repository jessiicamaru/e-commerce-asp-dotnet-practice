# Tasks: Product Variants

## Phase 1: Contracts

- [ ] T001 `OrderItemDto` gains `VariantId`; `StockAvailabilityChangedEvent` gains `VariantId` (both additive, `ProductId` stays)
- [ ] T002 [P] `catalog_pricing.proto`: `PriceVariants`, `DescribeVariants`, `PricedVariant`; nothing existing changed
- [ ] T003 [P] `cart_reading.proto`: `CartItem.variant_id = 3`

## Phase 2: Catalog — variants exist (US1)

- [ ] T004 `ProductVariant` and `VariantOption` entities in Catalog.Domain
- [ ] T005 EF configurations: unique `Sku`, unique `(VariantId, Name)`, FKs; migration `AddProductVariants` **with the id-reusing backfill** (research D2)
- [ ] T006 `IProductVariantRepository` (or variant methods on `IProductRepository`) + implementation
- [ ] T007 `AddProductVariantCommand` + validator + handler: unique SKU (409), identical options (409), option summary, product `Price`/`Sku` recompute
- [ ] T008 `UpdateProductVariantCommand`: price and `IsActive` only
- [ ] T009 `CreateProductCommand` also creates the first variant (id = a new v7 guid, options optional)
- [ ] T010 `ProductResponse` gains `priceVaries`, `variantCount`; `ProductDetailResponse` gains `variants`
- [ ] T011 `POST/PUT /api/products/{id}/variants…` in ProductsController
- [ ] T012 Catalog tests: backfill gives one variant per product with the product's id, price and sku; duplicate sku 409; identical options 409; `priceVaries`; a deactivated variant leaves the "from" price to the others

## Phase 3: Catalog — pricing and availability per variant (US1, US3)

- [ ] T013 `PriceVariants` and `DescribeVariants` in `CatalogPricingService`, all-or-nothing and per-item respectively
- [ ] T014 `RecordStockAvailability` keyed by variant (guarded update, older-observation guard kept), then recompute the product's flag in the same transaction
- [ ] T015 Catalog tests: pricing a variant, refusing an unsellable one, an unknown one; availability per variant and the derived product flag; `GetPrices` still works for a pre-existing product id

## Phase 4: Inventory (US3)

- [ ] T016 Comments and names: `StockItem.ProductId` / `StockReservation.ProductId` hold a **variant** id (research D9); `PUT /api/stock/{id}` documented as a variant id
- [ ] T017 `StockAvailabilityAnnouncer` sends `VariantId` (the row's id) as well as `ProductId`
- [ ] T018 Inventory tests: the announcement carries the variant id; reserving one variant never moves another's stock

## Phase 5: Cart (US2)

- [ ] T019 `CartLine.VariantId` (nullable) + migration; `AddToCart` takes a variant, or a product when it has exactly one (409 otherwise)
- [ ] T020 Cart's display uses `DescribeVariants`; a line shows the option summary; `CartReading` returns `variant_id`
- [ ] T021 Cart tests: a variant line, a product id with two variants is 409, a line written before the feature still resolves

## Phase 6: Order (US2)

- [ ] T022 `OrderItem.VariantId`, `Sku`, `OptionSummary` (nullable) + migration
- [ ] T023 `CheckoutPricing` prices **variants** through `PriceVariants` and freezes the words; `OrderSubmittedEvent` carries `VariantId`
- [ ] T024 Order tests: the line freezes sku and options; an unsellable variant refuses the whole order; a pre-feature cart line still checks out

## Phase 7: The storefront (US1, US2)

- [ ] T025 [P] `Product`/`Variant` types, `from` price on the card, variant chooser on the product page (no default when there is a choice — research D10)
- [ ] T026 [P] Cart and order pages show the option summary

## Phase 8: Evidence

- [ ] T027 Bruno: add a variant, list variants, the 409s, stock on a variant, checkout of a variant
- [ ] T028 `verify-saga.sh` passes unchanged
- [ ] T029 **The upgrade rehearsal**: against a database populated by the old images, upgrade and place an order naming no variant (FR-009, FR-010)
- [ ] T030 Negative controls: price from the product instead of the variant; reserve by product id; drop the backfill
- [ ] T031 Docs: CLAUDE.md (the id-reuse trap, the variant chain, test counts), README/docs where products are described
- [ ] T032 PR; CI green; squash-merge

## Dependencies

Phase 1 → 2 → 3, then 4, 5, 6 in that order (each keys by what the previous one exposes), then 7, then 8.
