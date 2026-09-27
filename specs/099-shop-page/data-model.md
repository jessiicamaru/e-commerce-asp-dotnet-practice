# Data Model: A shop has a page

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27

Both migrations only add nullable columns (expand only), so an earlier image can still read every row.

## Identity: `seller_profiles` (migration `20260927005825_AddShopDescription`)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Description` | `varchar(500)` null | **New.** The seller's own words, null when there are none. Trimmed on save, and a whitespace-only save stores null. |

## Catalog: `sellers` read model (migration `20260927010004_AddShopDescription`)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Description` | `varchar(500)` null | **New.** Filled from `SellerDescribedEvent`. |
| `DescriptionObservedAt` | `timestamptz` null | **New.** The `DescribedAt` of the stored description, used as its guard (research D4). Null until a description arrives. |

The existing columns are `SellerId` (the key), `ShopName`, `ObservedAt` (the name's guard), and `Suspended` with its own
guard (specs/095).

### Writes

**Recording a description** is one statement:

```sql
INSERT INTO sellers ("SellerId", "ShopName", "ObservedAt", "Description", "DescriptionObservedAt")
VALUES (@sellerId, '', @minValue, @description, @observedAt)
ON CONFLICT ("SellerId") DO UPDATE SET "Description" = EXCLUDED."Description",
                                       "DescriptionObservedAt" = EXCLUDED."DescriptionObservedAt"
 WHERE sellers."DescriptionObservedAt" IS NULL OR sellers."DescriptionObservedAt" < EXCLUDED."DescriptionObservedAt"
```

For a seller Catalog has not heard of yet, this creates a row with an empty name. The shop read treats that row as not
being a page yet.

### Reads

- **The shop**: the `sellers` row by id. The read answers 404 when there is no row, when `ShopName = ''`, or when
  `Suspended` is true.
- **The count on the shelf**: `products WHERE SellerId = @id AND ReviewStatus = 'Approved' AND IsActive AND NOT
  SellerSuspended`, the same rule as `Product.OnShelf`.
- **The listing**: `GET /api/products?sellerId=`, which goes through `GetPaginatedAsync(sellerId, listedOnly: true)`.

## State transitions

None. A description is a value, not a state. A suspended shop's page is a 404 through the existing suspension
(specs/095).
