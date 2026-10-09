# Data Model: A compare-at price per variant

## Columns (Catalog) - migration `AddCompareAtPrices`, expand-only

| Table | Column | Type | Notes |
| :-- | :-- | :-- | :-- |
| `product_variants` | `CompareAtPrice` | `decimal(18,2)` NULL | The default currency's compare-at, beside `Price` |
| `variant_prices` | `CompareAtAmount` | `decimal(18,2)` NULL | Another currency's, beside `Amount` |

Constraints:

- `CK_product_variants_CompareAtPrice`: `"CompareAtPrice" IS NULL OR "CompareAtPrice" > "Price"`
- `CK_variant_prices_CompareAtAmount`: `"CompareAtAmount" IS NULL OR "CompareAtAmount" > "Amount"`

Nothing existing is dropped, renamed or narrowed; an older image ignores both columns.

## Rules over time

| Change | Effect |
| :-- | :-- |
| Set a compare-at | Kept when above the price in that currency and representable; else refused (400) |
| Clear a compare-at | Set to null |
| Set a price below the compare-at | Compare-at kept |
| Set a price at or above it | Compare-at cleared in the same save (research D2) |
| Remove a non-default price | Its row goes, and its compare-at with it |

## Other declarations

- **Read cache** (specs/157): both tables are already in `CatalogueWrites`' pattern.
- **Personal data** (specs/111): no new table.
- **Audit**: `CompareAtPriceSet`, `CompareAtPriceRemoved` (Catalog).
