# Data Model: Search uses an index

> Written on 2026-09-27, after the feature merged (#158), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table or column changed.** One migration in `ecommerce_catalog_db` adds an extension, a function and three
indexes over columns that already existed. There are no states and no transitions.

Migration: `20260926092756_AddSearchIndexes`
([source](../../server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260926092756_AddSearchIndexes.cs)),
applied by Catalog at startup like the others.

---

## Extensions

| Extension | Before this feature | This migration |
| :--- | :--- | :--- |
| `unaccent` | Created by `20260922052231_AddTranslations` (specs/021) | `CREATE EXTENSION IF NOT EXISTS unaccent` - a no-op on any database that has run the earlier migration |
| `pg_trgm` | Absent | `CREATE EXTENSION IF NOT EXISTS pg_trgm` - provides `gin_trgm_ops` |

Both ship with the `postgres:16-alpine` image the compose file uses.

---

## Function `f_unaccent(text)`

```sql
CREATE OR REPLACE FUNCTION f_unaccent(text) RETURNS text
LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
AS $$ SELECT unaccent('unaccent'::regdictionary, $1) $$;
```

| Property | Why |
| :--- | :--- |
| `IMMUTABLE` | Required for an expression index; true because the dictionary is named rather than found at call time (research D1) |
| `PARALLEL SAFE` | It only calls `unaccent`, so a parallel plan may use it |
| `STRICT` | `NULL` in, `NULL` out without calling the body |

EF mapping, in `CatalogDbContext.OnModelCreating`:

```csharp
modelBuilder.HasDbFunction(typeof(SearchFunctions).GetMethod(nameof(SearchFunctions.Unaccent))!)
    .HasName("f_unaccent");
```

`SearchFunctions.Unaccent` throws `InvalidOperationException` if called in .NET: it exists only to be translated.

---

## Indexes

All three are GIN with the `gin_trgm_ops` operator class, created with `IF NOT EXISTS`.

| Index | Table | Expression | Column type |
| :--- | :--- | :--- | :--- |
| `IX_products_name_search` | `products` | `f_unaccent(lower("Name"))` | `Name` `character varying(200)` |
| `IX_products_sku_search` | `products` | `lower("Sku")` | `Sku` `character varying(50)` |
| `IX_product_translations_name_search` | `product_translations` | `f_unaccent(lower("Name"))` | `Name` `character varying(200)` |

The expressions must match the query's text exactly for the planner to use them - which is why the query calls
the mapped `f_unaccent` and `ToLower()` (translated to `lower(...)`) in that order, and why the SKU index has no
`f_unaccent`: the SKU arm does not unaccent.

The indexes are declared only in the migration's raw SQL, not in the EF model: neither the model snapshot nor the
migration's Designer file mentions them (checked 2026-09-27). A future migration scaffolded from the model will
therefore not try to drop them, because the model never knew of them - and equally, nothing in the model reminds
a developer they exist.

Existing indexes the search also relies on, unchanged: the primary keys (the UNION's ids are looked up by
`products."Id"`) and the unique `(ProductId, Language)` index on `product_translations`, which the planner uses
on a small table instead of the trigram index (see `SearchIndexTests`).

---

## The query the indexes serve

The shape `ProductRepository.GetPaginatedAsync` produces when a search term is given (illustrative, not EF's
verbatim text; `@pattern` is `SearchFunctions.ContainsPattern(term.Trim().ToLower())`):

```sql
... WHERE p."Id" IN (
    SELECT p0."Id" FROM products p0
     WHERE f_unaccent(lower(p0."Name")) LIKE f_unaccent(@pattern) ESCAPE '\'
        OR lower(p0."Sku") LIKE @pattern ESCAPE '\'
    UNION
    SELECT t."ProductId" FROM product_translations t
     WHERE t."Language" = @language
       AND f_unaccent(lower(t."Name")) LIKE f_unaccent(@pattern) ESCAPE '\'
)
```

The approval filter (`ReviewStatus = 'Approved'` for the public listing), the seller filter (`/mine`), the category
filter, the sort and the paging are applied around it exactly as before.

---

## `Down`

Drops `IX_product_translations_name_search`, `IX_products_sku_search`, `IX_products_name_search` and
`f_unaccent(text)`, in that order. **Leaves both extensions**: `unaccent` belongs to specs/021's migration and the
older search needs it.

---

## What did not change, and why that matters

- **No column, table or constraint.** The migration is additive under the constitution's Schema-evolution rule.
- **An earlier image still runs.** A Catalog image built before this feature queries with `strpos` over
  `unaccent(lower(...))`; both functions still exist, so its search works against the new schema - it just cannot
  use the new indexes. Rolling Catalog back is a redeploy, not a recovery.
- **Write cost.** Every insert and every name or SKU update on `products`, and every insert or name update on
  `product_translations`, now maintains a GIN index too. It was not measured - not recorded.
