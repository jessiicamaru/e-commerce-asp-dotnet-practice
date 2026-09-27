# Data model: Filter the catalogue by price and by what is in stock

No new table and no new column. There are two indexes, in migration `AddCatalogueFilterIndexes`:

| Index | Table | Columns | Serves |
| :-- | :-- | :-- | :-- |
| `IX_products_Price` | `products` | `Price` | A price bound in the default currency. |
| `IX_variant_prices_Currency_Amount` | `variant_prices` | `Currency`, `Amount` | A price bound in another currency: the grouped join by currency. |

Both only add, so an earlier image is unaffected.

The existing columns read:

- `products.Price` is the "from" price in the default currency, maintained by `RecomputeProductRollupAsync`.
- `products.Availability` is whether any active variant is in stock, maintained the same way.
- `variant_prices.Amount` and `Currency`, with `product_variants.IsActive`, give the "from" price in another
  currency.

There are no state transitions.
