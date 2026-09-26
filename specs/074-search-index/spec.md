# Feature Specification: Search uses an index

**Feature Branch**: `074-search-index` | **Created**: 2026-09-26 | **Issue**: #113 (closes it)

## Why

Diacritic-insensitive search (specs/021) calls `unaccent()` on every row and matches with `strpos`. `unaccent()`
is only STABLE, so PostgreSQL cannot index an expression that uses it, and a search therefore scans every
product and every translation. That is invisible at 50 products and the first thing to fix at 100,000.

## Requirements

- **FR-001** Keep exactly the same results, which the existing search tests pin:
  - diacritics ignored on both sides;
  - case ignored;
  - the name, the SKU and the requested language's translated name are all searched.
- **FR-002** An IMMUTABLE wrapper, `f_unaccent(text)`, which calls `unaccent` with its dictionary named. This is
  the standard pattern: naming the dictionary is what makes the IMMUTABLE declaration true.
- **FR-003** `pg_trgm` GIN indexes over `f_unaccent(lower("Name"))` for `products` and `product_translations`,
  and over `lower("Sku")`.
- **FR-004** The query matches with `LIKE '%' || f_unaccent(lower(@term)) || '%'`, which a trigram index can
  serve (`strpos` cannot). The term's own `%`, `_` and `\` are escaped, so a search for "50%" means those three
  characters.
- **FR-005** Evidence: `EXPLAIN ANALYZE`, before and after, of the query EF generates, on a catalogue of
  100,000 products. The plan uses the index, and the timing is recorded in the PR.

## Acceptance

- The existing search tests still pass.
- A new test: a term containing `%` or `_` is matched literally.
- The plan shows a Bitmap Index Scan on the new indexes.
