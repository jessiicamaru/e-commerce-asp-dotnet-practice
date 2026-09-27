# Data Model: A seller is told when a variant runs low

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27

## Inventory: `stock_items` (migration `AddLowStockThreshold`)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `LowStockThreshold` | `integer` null | **New.** The variant's own line. Null means the shop default (`Inventory:LowStock:DefaultThreshold`); 0 means never tell. CHECK `"LowStockThreshold" IS NULL OR "LowStockThreshold" BETWEEN 0 AND 100000`. |

The column is nullable and added without a default, so the change is expand only and an earlier image ignores it.

## The crossing (no stored state)

Inside `ReserveStockCommandHandler`'s transaction, with the rows locked `FOR UPDATE`:

```text
before    = stock.QuantityAvailable            (read under the lock)
stock.QuantityReserved += quantity
after     = stock.QuantityAvailable
threshold = stock.LowStockThreshold ?? DefaultThreshold
crossed   = threshold > 0 && before >= threshold && after < threshold
```

When `crossed` is true, `StockRanLowEvent` is staged before the one save.

## Catalog

There is no new table. The consumer reads `product_variants` (the product, the option summary) and `products`
(`SellerId`, `Name`), and publishes `UserNotificationRequested` through the outbox. The Catalog endpoint uses the
consumer outbox, whose inbox makes a redelivered event one notice.

## Notices (Shared `notification-kinds.json`)

| Kind | Required data | Placeholder |
| :-- | :-- | :-- |
| `StockRunningLow` | `product`, `left` | new `left` ← `["left"]` |
