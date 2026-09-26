# gRPC Contract: Two Price Lists

> Written on 2026-09-27, after the feature merged (#59), from the code at that merge, the pull request, docs/features/catalog.md and docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md) D3

`server/src/BuildingBlocks/Ecommerce.Contracts.Grpc/Protos/catalog_pricing.proto`, served by Catalog's
`CatalogPricingService` on its h2c port. Three fields added; no method, no field renumbered.

| Message | Field | Meaning |
| :--- | :--- | :--- |
| `PriceVariantsRequest` | `string currency = 3` | Which currency to price in. Empty = the shop's default, which is what an Order built before this feature sends |
| `DescribeVariantsRequest` | `string currency = 3` | The same, for Cart's display call |
| `PricedVariant` | `string currency = 8` | Which currency `price` is in, echoed so a caller never assumes |

Behaviour change on an existing field: `PricedVariant.price` (a decimal as a string, field 6) is now
**empty** when the variant has no price in the requested currency, and an empty price always comes with
`sellable = false` (field 7). It is never converted and never the default currency's amount relabelled.

## Callers

| Caller | Call | What it does with the answer |
| :--- | :--- | :--- |
| Order - `GrpcCatalogPrices` | `PriceVariants(ids, language, currency)` | Checkout and the quote: a variant with `sellable = false` refuses the checkout with 409 naming it (`CheckoutPricing`) |
| Cart - `GrpcCatalogProducts` | `DescribeVariants(ids, language, currency)` | The cart page: a line with no price in the currency is marked `NotSoldInCurrency`, distinct from `NoLongerAvailable`, because switching currency makes it buyable again |

## Compatibility

An older caller sends no `currency` and receives the default currency's prices - what it received before.
An older Catalog ignores the field and answers in the default currency; the caller then sees `currency`
empty. The older proto methods (`GetPrices`, `DescribeProducts`) were not changed.
