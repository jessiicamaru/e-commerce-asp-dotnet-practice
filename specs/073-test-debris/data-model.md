# Data Model: Test runs clean up after themselves

> Written on 2026-09-27, after the feature merged (#157), from the code at that merge, the pull request and
> docs/testing/testing-strategy.md.

**Feature**: [spec.md](spec.md)

**No table, column, index or migration changed.** The feature changes test tooling only.

What the runs now remove, and through which path:

| Row | Service / table | Removed by | Path |
| :-- | :-- | :-- | :-- |
| The run's product | Catalog `products` (+ variants, prices, translations, image) | Bruno `teardown`, `verify-saga.sh`, `verify-auth.sh` | `DELETE /api/products/{id}` |
| Its stock rows | Inventory `stock_items` | Inventory, on `ProductDeletedEvent` | asynchronous, via the broker |
| The seller's product | Catalog `products` | Bruno `teardown` | `DELETE /api/products/{sellerProductId}` |
| The run's category | Catalog `categories` | all three | `DELETE /api/categories/{id}` - after the products, or 409 |

Not removed: customers, sellers, addresses, carts, orders, payments, reviews, vouchers - records of what happened.
