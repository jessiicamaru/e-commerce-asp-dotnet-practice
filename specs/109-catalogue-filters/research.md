# Research: Filter the catalogue by price and by what is in stock

## D1 - Filter on the "from" price, the number the card shows

**Decision**: the bounds are compared with the product's "from" price in the asked currency, the same expression the
price sort already uses.

**Rationale**:

- A shopper filters on the price they see. A card showing "from 18,000,000 ₫" must appear under a 20,000,000 maximum.
- The sort and the filter agree, so sorting by price within a price range reads as a coherent list.

**Alternative rejected**: "any variant in range". It would list a product whose card shows a price outside the range
the shopper just set.

## D2 - Default currency: `products.Price`, with a btree index

**Decision**: add `IX_products_Price` and filter `products."Price"` directly in the default currency.

**Rationale**:

- `products.Price` is the maintained "from" price: `RecomputeProductRollupAsync` sets it to the minimum active
  variant price in every write that changes variants.
- A range on an indexed column is an index or bitmap scan.

## D3 - Other currencies: the product ids from a grouped join, never a per-product subquery

**Decision**: select `ProductId` from `variant_prices JOIN product_variants`, where the variant is active and the
currency matches, `GROUP BY ProductId HAVING min(Amount)` within the bounds. Then keep the products whose id is in that
set. Add `IX_variant_prices_Currency_Amount`.

**Rationale**:

- The sort's expression (a correlated `MIN` per product) would be a SubPlan run for every product. That is the shape
  specs/074 measured at 457 ms on 100,000 products and removed from the search.
- A grouped join, then an id semi-join, uses the currency index and no subplan. This follows specs/074's
  "UNION of ids" lesson.

**Alternative rejected**: a denormalised "from price" column per currency on `products`. It would be a second derived
value to keep in step, for a currency most shoppers do not browse in.

## D4 - In stock is `products.Availability`

**Decision**: `inStock=true` keeps `Availability = true`, the rollup of the active variants' stock.

**Rationale**: It is what the card's "Out of stock" badge reads, and it is maintained by Inventory's announcements.
It is a display filter, and checkout still reserves against Inventory.

**Alternative rejected**: asking Inventory. That would be a synchronous call per listing for a filter.

## D5 - Validation

**Decision**: bounds must be ≥ 0, and `minPrice ≤ maxPrice` when both are given. Otherwise 400 with the field named.

**Rationale**: A reversed range is a mistake better reported than silently answered with nothing.
