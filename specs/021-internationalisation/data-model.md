# Data Model: Speaking More Than One Language

## Catalog

### `product_translations` (new)

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | no | |
| `ProductId` | `uuid` | no | FK → `products`, cascade |
| `Language` | `varchar(10)` | no | `vi`, `en` - a tag, not an enum, so a third language is rows not a migration |
| `Name` | `varchar(200)` | no | |
| `Description` | `varchar(2000)` | yes | |

Unique on `(ProductId, Language)`.

### `variant_option_translations` (new)

| Column | Type | Null | Notes |
| :-- | :-- | :-- | :-- |
| `Id` | `uuid` | no | |
| `OptionId` | `uuid` | no | FK → `variant_options`, cascade |
| `Language` | `varchar(10)` | no | |
| `Name` | `varchar(50)` | no | `Kit` → `Bộ` |
| `Value` | `varchar(100)` | no | `Body only` → `Chỉ thân máy` |

Unique on `(OptionId, Language)`. Option values are read by customers as much as names are (FR-003).

### Unchanged, and now meaning "the default language"

`products.Name`, `products.Description`, `variant_options.Name/Value`. They are the fallback and what
an earlier image reads (research D2, D7).

## Order

`orders.Language varchar(10)`, nullable — the language the order was placed in. Null on orders placed
before this feature, which read as the default.

The line's frozen `ProductName` and `OptionSummary` (specs/020) now hold the text **in that language**.
Nothing about their shape changes.

## Resolution at read time

```text
name(product, lang) = translation(product, lang)?.Name
                   ?? product.Name                      -- the default-language text
optionSummary(variant, lang) = join(" · ", options.map(o =>
                   translation(o, lang)?.Name ?? o.Name : translation(o, lang)?.Value ?? o.Value))
search(term, lang) matches unaccent(lower(...)) over BOTH the translation in `lang` and the default text
```

## What is deliberately not translated

- **SKUs** — they are identifiers.
- **Category names** — the storefront shows them in the filter; recorded as the next thing to
  translate, and out of scope here so the feature stays reviewable.
- **Anything an administrator sees**, per the spec's assumptions.
