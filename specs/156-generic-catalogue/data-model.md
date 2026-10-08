# Data Model: A shop for anything, not only cameras

No table, column or migration. The new categories and products are rows in Catalog's existing tables (and stock rows in
Inventory's), created through the API by the seed.

The seed file format is unchanged from `cameras.json` (specs/023):

```text
{ "_about": [...],
  "categories": [ { slug, name, description, en: { name, description } } ],
  "products":   [ { sku, category, vi: { name, description }, en: { name, description },
                    variants: [ { sku, options: [ { vi: [name, value], en: [name, value] } ], vnd, usd, stock } ] } ] }
```
