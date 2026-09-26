# Data Model: A Catalogue of Real Cameras

> Written on 2026-09-27, after the feature merged (#60), from the code at that merge, the pull request
> and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No column, table, key, index or constraint changed.** One data-only migration in
`ecommerce_catalog_db`; everything else the feature writes is ordinary rows, written through the API.

---

## Migration `20260922161858_NormaliseOptionSummaryOrder` (Catalog)

```sql
UPDATE product_variants v
SET "OptionSummary" = COALESCE((
    SELECT string_agg(o."Name" || ': ' || o."Value", ' · ' ORDER BY lower(o."Name"))
    FROM variant_options o
    WHERE o."VariantId" = v."Id"
), '');
```

- Rewrites every variant's stored summary with its options ordered by name, case-insensitively - the
  same order `ProductVariant.Summarise` now produces (research D6). A variant with no options gets `''`.
- **`Down` is empty on purpose.** The earlier order was whatever the database returned, which cannot be
  restored; an earlier image reads the rewritten summaries perfectly well, because only their word order
  changed.
- **Orders keep the text they froze.** `order_items` in Order's database are not touched - "because it
  is theirs".

Tables read: `variant_options` (`VariantId`, `Name`, `Value`). Column written:
`product_variants."OptionSummary"`.

---

## The seed file: `server/seed/cameras.json`

```text
_about        lines of text - the approximate-price warning, the two-lists note, no images
categories[]  { slug, name, description }                       (Vietnamese only)
products[]    { sku, category (slug),
                vi: { name, description }, en: { name, description },
                variants[]: { sku, options[]: { vi: [name, value], en: [name, value] },
                              vnd, usd, stock } }
```

At the merge: 2 categories (`may-anh-mirrorless`, `may-anh-compact`), 14 products, 23 variants, 195 units
of stock. Option axes: `Bộ` / `Kit` and `Màu` / `Colour`.

## What the seeder writes, and where

| Row | Service / table | Written by | Idempotent by |
| :--- | :--- | :--- | :--- |
| Category | Catalog `categories` | `POST /api/categories` | slug (skipped if present) |
| Product + its first variant | Catalog `products`, `product_variants`, `variant_options` | `POST /api/products` (Vietnamese text, dong price, first variant's options) | product SKU (skipped if present) |
| English product text | Catalog `product_translations` | `PUT /api/products/{id}/translations/en` | upsert |
| Further variants | Catalog `product_variants`, `variant_options` | `POST /api/products/{id}/variants` | variant SKU |
| Dollar price | Catalog `variant_prices` | `PUT /api/products/{id}/variants/{variantId}/prices/USD` | upsert |
| Stock | Inventory `stock_items` (row created from Catalog's event) | `PUT /api/stock/{variantId}` | absolute value |
| English option words | Catalog `variant_option_translations` | `PUT /api/products/{id}/options/{optionId}/translations/en` | upsert |

**The first variant's SKU in the file is never stored.** The first variant is created with the product,
reuses the product's id and carries the product's SKU (specs/020) - so `SONY-A7M4-BODY` in the file is
stored as `SONY-A7M4`. The seeder treats index 0 as the product's own variant for that reason, rather
than looking it up by its file SKU and adding a duplicate shape.

## What did not change

- **No new column for option order.** The order is computed (by name), not stored.
- **Order lines** keep the summaries they froze before the fix.
- **Category names** stay Vietnamese only (translated in specs/026).
