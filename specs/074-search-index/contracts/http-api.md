# HTTP Contract: Search uses an index

> Written on 2026-09-27, after the feature merged (#158), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

**No request or response changed.** This file documents the two endpoints whose search this feature
re-implemented, so the reader can see exactly what did change: how the term is matched, and what it costs. No
message, gRPC service or gateway route was added or changed.

**Base**: Catalog on `http://localhost:5057`, reached through the gateway on `:5000` by the existing
`catalog-products-route` (`/api/products/{**catch-all}`).

Errors follow the project's RFC 7807 shape via `GlobalExceptionHandler` - see
[error-handling-and-shared-building-block.md](../../../docs/architecture/error-handling-and-shared-building-block.md).
This feature adds no error.

---

## `GET /api/products` - anonymous

The public listing. Only approved products (specs/045).

**Query** (bound to `GetProductsQuery`): `pageNumber` (default 1), `pageSize` (default 12), `categoryId`,
**`searchTerm`**, `sortBy` (`name_desc`, `price_asc`, `price_desc`; default by name). The language comes from
`?lang=` then `Accept-Language`; the currency from `?currency=` then `X-Currency` - both unchanged.

> The search parameter is `searchTerm`, not `search`: `?search=` is not bound and returns the unfiltered listing.

**Response** `200`, unchanged:

```json
{
  "items": [ /* ProductResponse, as before */ ],
  "pageNumber": 1,
  "totalPages": 1,
  "totalCount": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

### What `searchTerm` means (unchanged in effect)

A product is included when, after the term is trimmed and lower-cased:

- its **name**, lower-cased and unaccented, contains the unaccented term; or
- its **SKU** (the product's, not a variant's), lower-cased, contains the term; or
- its **translated name in the requested language**, lower-cased and unaccented, contains the unaccented term.

Diacritics are ignored on both sides of the name and translation matches, so `may anh` finds "Máy ảnh" and
`máy ảnh` finds a product entered without accents. Every character of the term is literal: `50%` finds
"Sale 50%" and not "Sale 5000"; `a_b` finds "a_b" and not "axb"; a backslash matches a backslash. A blank term is
no filter.

### What changed underneath

| | Before (specs/021) | After (this feature) |
| :-- | :-- | :-- |
| Name match | `strpos(unaccent(lower("Name")), unaccent(@term)) > 0` | `f_unaccent(lower("Name")) LIKE f_unaccent(@pattern) ESCAPE '\'` |
| Translation match | a correlated `EXISTS` inside the same `OR` | a `UNION` of the matching translation's `ProductId`s |
| `%`, `_`, `\` in the term | literal (`strpos` has no wildcards) | literal (escaped by `SearchFunctions.ContainsPattern`) |
| Index used | none - a sequential scan of products and translations | `IX_products_name_search`, `IX_products_sku_search`, `IX_product_translations_name_search` |
| Narrow search, 100,000 products (PR #158) | 452 ms | 1.2 ms |
| Broad search, 10,000 matches (PR #158) | 426 ms | 81 ms |

The "before" SQL is described from the pre-merge code (`string.Contains` over an `unaccent` expression, which
Npgsql translates to `strpos`); the PR names `strpos(unaccent(lower(...)))` as the old match.

---

## `GET /api/products/mine` - `Seller`

The caller's own listings, whatever their review status (specs/027). Query: `pageNumber`, `pageSize`,
**`searchTerm`**, `sortBy`; no seller id - it comes from the token. It calls the same
`ProductRepository.GetPaginatedAsync` (with `listedOnly: false`), so `searchTerm` has exactly the meaning and the
indexes described above. Response unchanged.

---

## Callers

- The storefront catalogue (`client/src/pages/catalog/`), with the term in `?q=` of its own URL.
- The seller's product list (`client/src/pages/shop-products/`), through `/mine`.
- The voucher product picker (`client/src/components/voucher/product-picker/`), eight at a time.
- Bruno: `bruno/product/search without diacritics.yml` (`?searchTerm=may anh bruno&lang=vi`).

None of them changed.
