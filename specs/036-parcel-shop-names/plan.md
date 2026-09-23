# Implementation Plan: Which shop each parcel comes from

**Branch**: `036-parcel-shop-names` | **Spec**: [spec.md](spec.md)

## Technical Context

- **Catalog**: `CatalogPricingService` takes `ISellerRepository`; `PriceVariants` and `DescribeVariants`
  look the names up once per request and set `seller_name`.
- **Order**: `order_items.SellerName varchar(100) NULL` (migration `AddOrderItemSellerName`);
  `CatalogPrice`/`PricedLine` carry it; checkout freezes it; order lines, parcels and the quote return it.
- **Client**: order lines say "Sold by …"; parcel headings name the shop, or "The shop".

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Order reads its own column; no new call. The name rides an answer checkout already asks for. |
| II - Clean Architecture | The proto field is read in Infrastructure; Application sees `string?`. |
| III - Atomic writes | Written in the order's own save. |
| IV - Identity from the token | Unchanged. Only a public display name is disclosed. |
| V - Evidence | Tests for: frozen at checkout, unchanged by a later rename, unknown recorded as null, one batched lookup in Catalog. |
| Schema compatibility | One nullable column. Additive. |

No Complexity Tracking entries.

## Verification

Order and Catalog tests; client tests; Bruno; `verify-saga.sh`; end to end with a fresh two-seller
order, then a shop rename, then the old order re-read.
