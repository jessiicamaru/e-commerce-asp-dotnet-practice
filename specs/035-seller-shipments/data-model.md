# Data model: Each seller ships their own part

## `order_shipments` (new, Order database)

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | uuid | no | |
| `OrderId` | uuid | no | FK → orders, cascade |
| `SellerId` | uuid | yes | Whose part; null = the shop's own goods |
| `Status` | varchar(16) | no | `Pending` / `Preparing` / `Shipped` |
| `TrackingReference` | varchar(100) | yes | Set when shipped |
| `UpdatedAt` | timestamptz | no | |

Unique `IX_order_shipments_OrderId_SellerId` **NULLS NOT DISTINCT**. Index on `SellerId`.

## Migration `AddOrderShipments`

1. Create the table and indexes.
2. Backfill: one part per `(OrderId, SellerId)` found in `order_items`, status from the order -
   `Preparing` → `Preparing`, `Shipped` → `Shipped` (with the order's tracking reference), anything
   else → `Pending`.

Additive: an older image ignores the table. `orders.Status` keeps its values.

## `orders` - unchanged columns, new meaning

`Status` and `TrackingReference` become a **summary** of the parts, written by the same transaction
that moves a part (research D4).
