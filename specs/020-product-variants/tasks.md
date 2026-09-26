# Tasks: Product Variants

## Phase 1: Contracts

- [X] T001 `OrderItemDto` gains `VariantId`; `StockAvailabilityChangedEvent` gains `VariantId` (both additive, `ProductId` stays)
- [X] T002 [P] `catalog_pricing.proto`: `PriceVariants`, `DescribeVariants`, `PricedVariant`; nothing existing changed
- [X] T003 [P] `cart_reading.proto`: `CartItem.variant_id = 3`

## Phase 2: Catalog — variants exist (US1)

- [X] T004 `ProductVariant` and `VariantOption` entities in Catalog.Domain
- [X] T005 EF configurations: unique `Sku`, unique `(VariantId, Name)`, FKs; migration `AddProductVariants` **with the id-reusing backfill** (research D2)
- [X] T006 `IProductVariantRepository` (or variant methods on `IProductRepository`) + implementation
- [X] T007 `AddProductVariantCommand` + validator + handler: unique SKU (409), identical options (409), option summary, product `Price`/`Sku` recompute
- [X] T008 `UpdateProductVariantCommand`: price and `IsActive` only
- [X] T009 `CreateProductCommand` also creates the first variant — with the PRODUCT’s id, like the backfill (options optional)
- [X] T010 `ProductResponse` gains `priceVaries`, `variantCount`; `ProductDetailResponse` gains `variants`
- [X] T011 `POST/PUT /api/products/{id}/variants…` in ProductsController
- [X] T012 Catalog tests: backfill gives one variant per product with the product's id, price and sku; duplicate sku 409; identical options 409; `priceVaries`; a deactivated variant leaves the "from" price to the others

## Phase 3: Catalog — pricing and availability per variant (US1, US3)

- [X] T013 `PriceVariants` and `DescribeVariants` in `CatalogPricingService`, all-or-nothing and per-item respectively
- [X] T014 `RecordStockAvailability` keyed by variant (guarded update, older-observation guard kept), then recompute the product's flag in the same transaction
- [X] T015 Catalog tests: pricing a variant, refusing an unsellable one, an unknown one; availability per variant and the derived product flag; `GetPrices` still works for a pre-existing product id

## Phase 4: Inventory (US3)

- [X] T016 Comments and names: `StockItem.ProductId` / `StockReservation.ProductId` hold a **variant** id (research D9); `PUT /api/stock/{id}` documented as a variant id
- [X] T017 `StockAvailabilityAnnouncer` sends `VariantId` (the row's id) as well as `ProductId`
- [X] T018 Inventory tests: the announcement carries the variant id; reserving one variant never moves another's stock

## Phase 5: Cart (US2)

- [X] T019 `CartLine.VariantId` (nullable) + migration; `AddToCart` takes a variant, or a product when it has exactly one (409 otherwise)
- [X] T020 Cart's display uses `DescribeVariants`; a line shows the option summary; `CartReading` returns `variant_id`
- [X] T021 Cart tests: a variant line, a product id with two variants is 409, a line written before the feature still resolves

## Phase 6: Order (US2)

- [X] T022 `OrderItem.VariantId`, `Sku`, `OptionSummary` (nullable) + migration
- [X] T023 `CheckoutPricing` prices **variants** through `PriceVariants` and freezes the words; `OrderSubmittedEvent` carries `VariantId`
- [X] T024 Order tests: the line freezes sku and options; an unsellable variant refuses the whole order; a pre-feature cart line still checks out

## Phase 7: The storefront (US1, US2)

- [X] T025 [P] `Product`/`Variant` types, `from` price on the card, variant chooser on the product page (no default when there is a choice — research D10)
- [X] T026 [P] Cart and order pages show the option summary

## Phase 8: Evidence

- [X] T027 Bruno: add a variant, list variants, the 409s, stock on a variant, checkout of a variant
- [X] T028 `verify-saga.sh` passes unchanged
- [X] T029 **The upgrade rehearsal**: against a database populated by the old images, upgrade and place an order naming no variant (FR-009, FR-010)
- [X] T030 Negative controls: price from the product instead of the variant; reserve by product id; drop the backfill
- [X] T031 Docs: CLAUDE.md (the id-reuse trap, the variant chain, test counts), README/docs where products are described
- [X] T032 PR; CI green; squash-merge — *Merged as #57; every check green (success, publish skipped on the PR). Ticked on 2026-09-27 from the PR's record.*

## Dependencies

Phase 1 → 2 → 3, then 4, 5, 6 in that order (each keys by what the previous one exposes), then 7, then 8.

## What actually happened

- **182 tests pass** (Catalog 29, Order 50, Identity 50, Inventory 26, Cart 14, Payment 13). New:
  `VariantTests`, `VariantLineTests`, `VariantCheckoutTests`, and a migration test that upgrades its own
  database from the previous version and asserts the backfilled variant reuses the product's id.
- Negative controls, each restored: drop the backfill → the migration test fails; key cart lines by
  product → 2 fail; freeze no sku or options on the order line → 1 fails.
- **The upgrade was rehearsed on the real development database**, not a fresh one: 76 products, 76
  variants, and all 76 with the product's id, sku and price carried over.
- End to end through the gateway: a product with two shapes, stock on each, and buying the kit moved
  **the kit's** stock (2 → 1) and left the body's alone.
- **A bug this found, and the only one it could have**: the first attempt moved the WRONG variant's
  stock, because the Orchestrator relays the items and had not been rebuilt (research D11).
- `verify-saga.sh` passes unchanged. **Bruno 67/67, 92 tests**, with five new requests.
- Two things the requirement did not anticipate, both built: a new variant had nowhere to keep stock
  (Catalog now publishes `ProductVariantCreatedEvent`, Inventory registers the row), and the cart's
  unique index had to become variant-aware with `NULLS NOT DISTINCT`.
