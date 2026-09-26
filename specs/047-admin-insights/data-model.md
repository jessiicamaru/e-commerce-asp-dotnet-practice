# Data Model: Admin insights

> Written on 2026-09-27, after the feature merged (#99, #101), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

One new table, `product_views`, in `ecommerce_catalog_db` (host port 5433), created by one migration,
`20260923214827_AddProductViews` in `Ecommerce.Catalog.Infrastructure/Migrations/`. Everything else the feature
does is a read of tables that already existed, in Order and Identity; neither service gained a migration. #101
changed the storefront only.

---

## `product_views` (Catalog)

How many times shoppers opened a product's page on one day. A counter, not a log: "the question is 'what do
people look at', not 'who looked'" (`ProductView`'s comment, research D4). Mapped by
`ProductViewConfiguration`.

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `ProductId` | `uuid` | PK (part 1), FK → `products.Id`, `ON DELETE CASCADE` | The product viewed - the product, not a variant |
| `Day` | `date` | PK (part 2), indexed | The UTC date of the view (`DateOnly.FromDateTime(DateTime.UtcNow)`) |
| `Views` | `integer` | not null | Views counted that day |

**Keys and indexes**:

- `PK_product_views` on (`ProductId`, `Day`) - the conflict target of the upsert, so one row per product per day.
- `IX_product_views_Day` on `Day` - the most-viewed read filters by a day range across every product.
- `FK_product_views_products_ProductId`, cascade: deleting a product deletes its counts, so a deleted product
  drops out of "most viewed" (spec, Edge Cases).

**How it is written**: only by `ProductViewRepository.RecordAsync`, one statement per counted view (research D3):

```sql
INSERT INTO product_views ("ProductId", "Day", "Views") VALUES (@productId, @day, 1)
ON CONFLICT ("ProductId", "Day") DO UPDATE SET "Views" = product_views."Views" + 1
```

There is no check constraint on `Views`; the only writer starts it at 1 and adds 1.

**How it is read**: `TopAsync(from, to, limit)` sums `Views` per `ProductId` over `Day BETWEEN from AND to`
(both days included), orders by the sum descending, takes `limit`, and joins `products` for `Name` (the
default-language name). It does not re-check `IsListed`.

**Migration**: `20260923214827_AddProductViews` - `CreateTable product_views`, `CreateIndex IX_product_views_Day`;
`Down` drops the table. Additive only: an earlier Catalog image ignores the table, so a rollback needs nothing
(constitution, Schema evolution).

---

## Read, not changed (Order, `ecommerce_order_db`)

| Table | Columns read | For |
| :-- | :-- | :-- |
| `orders` | `Status`, `CreatedAt`, `Currency`, `TotalAmount`, `UserId` | The sale filter (`Status` in `Paid`, `Completed`, `Preparing`, `Shipped`; `CreatedAt` in `[from, to)`), revenue per UTC day per currency, buyers |
| `order_items` | `OrderId`, `ProductId`, `ProductName`, `Quantity`, `UnitPrice` | Units and goods revenue per product per currency; the name from the line of the most recent order |

The groupings run in PostgreSQL through LINQ (`GroupBy` on `CreatedAt.Date` and `Currency`; on `ProductId` and
`Currency`; on `UserId` and `Currency`). `Status` is stored as text (`HasConversion<string>()`), so the filter is
a string comparison against the four names. `Currency` is null on orders from before specs/022; the handler maps
it to the default currency.

No index was added for these reads (none recorded as needed or measured).

## Read, not changed (Identity, `ecommerce_identity_db`)

| Table | Columns read | For |
| :-- | :-- | :-- |
| `users` | `Id`, `Email`, `FirstName`, `LastName` | The lookup, by id |
| `users` | `LockedUntil`, `BannedAt` | Locked (`LockedUntil > now`) and banned (`BannedAt IS NOT NULL`) counts |
| `roles`, `user_roles` | role name, membership | A count per role (`r.Users.Count`) |

## Read, not changed (Catalog)

`products` - `Name` for the most-viewed list, and `IsListed` (with `SellerId`) when deciding whether a view
counts.

---

## What did not change, and why

- **No status, column or table in Order.** A sale is defined by a list of existing statuses (research D5), and
  the day by the existing `CreatedAt`. A payment time was out of scope; specs/072 later added `orders.PaidAt`
  as an expand-only column.
- **No new column on `products`.** The counts live only in `product_views`; every read asks "over a period", so
  a lifetime total on the product row was not needed (whether one was considered is not recorded).
- **No message and no read model.** Every figure is read live from the service that owns it (research D1).

## Later changes to this table (one line each)

- specs/082: `Day` is the shop's date (`Insights:TimeZone`) instead of UTC; rows written before kept their UTC day.
- specs/086: a new `product_viewers` table claims one view per viewer per day before the increment runs.
