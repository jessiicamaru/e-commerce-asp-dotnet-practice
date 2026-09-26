# gRPC Contract: Product review before sale

> Written on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Feature**: [spec.md](../spec.md) | **Decision**: [research.md D2](../research.md)

**`catalog_pricing.proto` did not change.** No field, message or rpc was added, so no caller was
rebuilt. What changed is what one existing field means.

## `sellable` now also means "approved"

`server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/catalog_pricing.proto`, served by
`CatalogPricingService` in `Ecommerce.Catalog.WebApi/Grpc/`:

| rpc | Field | Before | After (specs/045) |
| :-- | :-- | :-- | :-- |
| `GetPrices` | `sellable` (4) | `product.IsActive` | `product.IsActive && product.IsListed` |
| `DescribeProducts` | `sellable` (4) | `product.IsActive` | `product.IsActive && product.IsListed` |
| `PriceVariants` | `sellable` (7) | `variant.Sellable && price is not null` | unchanged expression; `ProductVariant.Sellable` now requires `Product.IsListed` |
| `DescribeVariants` | `sellable` (7) | as `PriceVariants` | as `PriceVariants` |

`ProductVariant.Sellable` went from `IsActive && (Product?.IsActive ?? true)` to
`IsActive && (Product is null || (Product.IsActive && Product.IsListed))`.

## What callers do with it - unchanged code

- **Order** (`CheckoutPricing`, used by checkout and the quote): a variant with `sellable = false` is
  refused with 409 `Not currently for sale: <names>.` - the path that already refused an inactive
  product.
- **Cart** (`GetMyCartQueryHandler`): the line is shown with status `NotForSale`.

A pending, rejected or taken-down product therefore cannot be bought, and nobody outside Catalog knows
why - which is the point of D2: review is Catalog's fact.

## Not changed

`catalog_ownership.proto` (specs/031, asked by Inventory before a seller stocks a variant) answers
ownership, not sellability, and was left alone: a seller may stock a product that is still waiting for
review.
