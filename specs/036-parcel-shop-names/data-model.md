# Data Model: Which shop each parcel comes from

> Written on 2026-09-27, after the feature merged (#80), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

## `order_items` — one column (Order database)

Migration `20260923122805_AddOrderItemSellerName`.

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `SellerName` | `character varying(100)` | yes | The selling shop's name as Catalog's `sellers` read model knew it at checkout. **Null** for the shop's own goods, for a seller Catalog had not heard of yet, for a Catalog too old to send it, and for every order placed before this migration. Never `""`, never an id. |

100 matches the width of Catalog's own shop-name column. No index, no constraint: the column is read with
its line and never searched.

**Written once**, by `SubmitOrderCommandHandler` in the order's own save, from `CatalogPrice.SellerName`
through `PricedLine.SellerName`. **Never written again** - not by a rename, not by a backfill (research
D1, D4). It sits beside `order_items.SellerId` (specs/034), which says whose the line is; this says what
they were called.

**Schema compatibility**: additive - one nullable column. An image from before #80 does not select it
and writes new lines with it null, which read as "no name recorded".

`Down` drops the column.

## Catalog — nothing changed

`CatalogPricingService` reads the existing `sellers` table (`ISellerRepository.GetNamesAsync`), once per
`PriceVariants` or `DescribeVariants` request, for the distinct seller ids of the variants asked about.

## Derived, not stored

| Response field | Derived from |
| :-- | :-- |
| order line `sellerName` | `order_items.SellerName` |
| quote line `sellerName` | the live pricing answer (not yet frozen - the quote places nothing) |
| parcel `sellerName` | the first non-null `SellerName` among the lines whose `SellerId` equals the part's |
| parcel `isShop` | the part's `SellerId` is null |

## States

None. The name has one state: recorded or not.
