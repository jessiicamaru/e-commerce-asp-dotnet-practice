# Data Model: The catalogue's public reads, served from memory

No table, column or migration. The cache lives in Catalog's process memory:

```text
entry key  = path + every query key + negotiated language + negotiated currency
entry      = status 200, headers, body; tag "catalogue"; expires after Caching:CatalogueSeconds (30)
evicted    = all entries with tag "catalogue", after any committed write to:
             products, product_variants, variant_prices, product_translations, variant_options,
             variant_option_translations, categories, category_translations, sellers
```
