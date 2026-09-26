# Contracts: A deleted product's holds are released with its stock

**Feature**: [spec.md](../spec.md)

**No HTTP, message or gRPC contract changes.** This file records the one message the change hangs on and what its
consumer now does.

## `Ecommerce.Contracts.Catalog.ProductDeletedEvent` (unchanged)

Published by Catalog through its outbox when an administrator deletes a product (`DELETE /api/products/{id}`,
specs/024). Carries `VariantIds` - every variant of the product.

| Consumer | Service | Before | After |
| :-- | :-- | :-- | :-- |
| `ProductDeletedConsumer` → `ForgetProductCommand` | Inventory | deletes the variants' `stock_items` rows | deletes them **and** releases their `Held` reservations ("Product deleted"), in one transaction |

Idempotent as before: a redelivery deletes nothing and releases nothing (the release is guarded on `Held`).

## Messages not published

The release publishes nothing: no `StockAvailabilityChangedEvent` (the stock rows are gone and Catalog has deleted
the product), no audit entry (a system consequence of the deletion, which Catalog already recorded), no message to
Order or the saga (the order is not failed or cancelled by this).

## HTTP

`GET /api/stock/{variantId}` for a deleted variant is 404, as since specs/024. No reservation endpoint changes.
