# Data model: Filter the catalogue by price and by what is in stock

No new table and no new column. There are two indexes, in migration `AddCatalogueFilterIndexes`:

| Index | Table | Columns | Serves |
| :-- | :-- | :-- | :-- |
| `IX_products_on_shelf_Price` | `products` | `Price`, partial: on the shelf (`Approved`, active, shop open) | A price bound in the default currency - and its price sort. Partial with the listing's own predicate, so the planner prefers it (a plain one lost to the review index on a small table). |
| `IX_variant_prices_Currency_Amount` | `variant_prices` | `Currency`, `Amount` | A price bound in another currency: the grouped join by currency. |

Both only add, so an earlier image is unaffected.

The existing columns read:

- `products.Price` is the "from" price in the default currency, maintained by `RecomputeProductRollupAsync`.
- `products.Availability` is whether any active variant is in stock, maintained the same way.
- `variant_prices.Amount` and `Currency`, with `product_variants.IsActive`, give the "from" price in another
  currency.

There are no state transitions.
