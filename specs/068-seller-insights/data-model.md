# Data Model: A seller sees how their shop is doing

> Written on 2026-09-27, after the feature merged (#152), from the code at that merge, the pull request and
> docs/features/seller-insights.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

**No table, column, index or migration was added or changed.** The feature reads existing tables. What follows is
which columns it reads and how.

## Order (`ecommerce_order_db`) - read only

| Table | Columns read | Used for |
| :-- | :-- | :-- |
| `orders` | `Id`, `Status`, `CreatedAt`, `Currency` | Sold statuses (`OrderInsights.Sold`: Paid, Completed, Preparing, Shipped) inside the period; the day; the currency |
| `order_items` | `SellerId`, `ProductId`, `ProductName`, `Quantity`, `UnitPrice` | The caller's lines; revenue `Quantity × UnitPrice` (before tax - `TaxAmount` is not read) |
| `order_shipments` | `OrderId`, `SellerId` | The seller's part of the order |
| `parcel_returns` | `ShipmentId`, `Status` | A part whose return is `Received` is left out (specs/066) |

Rows produced (existing records, reused): `RevenueRow(Day, Currency, Revenue, Orders)` with `Orders` = distinct order
ids per day and currency; `ProductSalesRow(ProductId, Name, Currency, Units, Revenue)` with the name of the newest
line.

## Catalog (`ecommerce_catalog_db`) - read only

| Table | Columns read | Used for |
| :-- | :-- | :-- |
| `products` | `Id`, `Name`, `SellerId`, `RatingAverage`, `RatingCount` | The caller's products; the weighted rating |
| `product_views` | `ProductId`, `Day`, `Views` | Views per product inside `[from, to]` (whole days) |

New read records in `IProductViewRepository.cs`:

```csharp
public record SellerProductInsight(Guid ProductId, string Name, int Views, decimal? RatingAverage, int RatingCount);
public record SellerProductInsights(int Views, decimal? RatingAverage, int RatingCount, List<SellerProductInsight> Products);
```

`Views` and the rating are totals over **all** the seller's products; `Products` is the `Limit` most viewed (ties by
review count, then name).

## Storefront types

`SellerProductInsight` and `SellerProductInsights` in `client/src/services/insights/types.ts`, mirroring the above; the
revenue and top products reuse `Revenue` and `TopProduct`.

## Later changes to these reads

- specs/069: revenue became `Quantity × UnitPrice - ShopDiscount` (a seller pays for their own voucher).
- specs/072: the day became `PaidAt ?? CreatedAt`.
