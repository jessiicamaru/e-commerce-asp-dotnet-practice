# Quickstart: Validating that search uses an index

> Written on 2026-09-27, after the feature merged (#158), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md) | **Data**: [data-model.md](./data-model.md)

Each scenario maps to a success criterion. Scenarios 1 and 2 are the automated checks; 3 to 6 are against a
running stack; 7 is how the PR's measurement was taken, and it needs data this repository does not contain.

---

## Prerequisites

```bash
cd server
docker compose up -d        # ecommerce-catalog-db on 5433 among the rest
./start-dev.sh              # or start-dev.ps1; Catalog applies AddSearchIndexes at startup
```

`server/.env` with `DB_USER` / `DB_PASSWORD` as usual. For the tests, the Catalog test database on 5433 and the
S3 variables the Catalog tests need (see CLAUDE.md).

---

## Scenario 1 - The results did not change, and "50%" is literal (SC-004, SC-005)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests \
  --filter "FullyQualifiedName~TranslationTests"
```

**Expected**: all pass, including `Searching_ignores_diacritics_and_looks_at_both_the_translation_and_the_original`
(unchanged since specs/021) and `A_term_with_percent_or_underscore_is_matched_literally` (new).

---

## Scenario 2 - The query is one the indexes can answer (SC-006)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests \
  --filter "FullyQualifiedName~SearchIndexTests"
```

**Expected**: `The_search_query_is_one_its_trigram_indexes_can_answer` passes. It fails if the query uses
`unaccent` instead of `f_unaccent`, `strpos` instead of `LIKE`, or puts the translations back into an `OR EXISTS`
(a `SubPlan` in the plan) - the PR's mutation checks.

---

## Scenario 3 - The function and the indexes exist

```bash
docker exec ecommerce-catalog-db psql -U "$DB_USER" -d ecommerce_catalog_db -c \
  "SELECT indexname, indexdef FROM pg_indexes WHERE indexname LIKE '%_search';"
docker exec ecommerce-catalog-db psql -U "$DB_USER" -d ecommerce_catalog_db -c \
  "SELECT proname, provolatile FROM pg_proc WHERE proname = 'f_unaccent';"
```

**Expected**: three rows - `IX_products_name_search`, `IX_products_sku_search`,
`IX_product_translations_name_search`, each `USING gin (... gin_trgm_ops)` - and `f_unaccent` with
`provolatile = i` (immutable). The PR reports this check passing on a rebuilt Catalog container.

---

## Scenario 4 - Search without diacritics through the gateway (User Story 1)

```bash
curl -fsS "http://localhost:5000/api/products?searchTerm=may%20anh&lang=vi&pageSize=12" | jq '.totalCount, [.items[].name]'
```

**Expected**: `200`, and products named "Máy ảnh ..." among the results - the same answer as before the
migration. The PR reports `GET /api/products?searchTerm=may anh` answering against the rebuilt container.

---

## Scenario 5 - A literal percent sign (User Story 2)

As an administrator, create a product named "Sale 50% qs" and another named "Sale 5000 qs" (Bruno's
`product/create product` or `POST /api/products`), then:

```bash
curl -fsS "http://localhost:5000/api/products?searchTerm=50%25%20qs" | jq '[.items[].name]'
```

**Expected**: only "Sale 50% qs". (`%25` is the URL encoding of `%`.) Delete both products afterwards
(`DELETE /api/products/{id}`), or run `seed/clean-test-debris.py`.

Not recorded whether this scenario was run by hand; the automated test in scenario 1 covers the same case.

---

## Scenario 6 - Bruno (SC-007)

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: every request green, including `product/search without diacritics`. The PR reports 232/232
requests and 379/379 assertions through the storefront.

---

## Scenario 7 - The measurement (SC-001 to SC-003)

How the PR measured: a scratch database built with the real migrations, seeded with 100,000 products (names in
Vietnamese and English with diacritics) and 50,000 Vietnamese translations; the query was exactly the SQL EF
generates, taken from EF's own log (enable `Microsoft.EntityFrameworkCore.Database.Command` at `Information`);
`EXPLAIN ANALYZE` of it before the migration and after.

The seeding script is **not in the repository** - not recorded beyond the description above. To repeat it, seed a
scratch database the same way, then in `psql`:

```sql
EXPLAIN ANALYZE <the SELECT EF logged for GET /api/products?searchTerm=...&lang=vi>;
```

**Expected** (the PR's figures):

| Query (a page of 12, by name) | Before | After |
| :-- | :-- | :-- |
| narrow, 1 match | Seq Scan on products and on translations, 452 ms | BitmapOr (name, sku) + translations index, 1.2 ms |
| broad (`may anh`), 10,000 matches | Seq Scan, 426 ms | the same three index scans, 81 ms |
| count, narrow | Seq Scan, 516 ms | 4.2 ms |

A plan that still shows `Seq Scan on products` together with a `SubPlan` is the `OR EXISTS` shape (research D5);
it measured 457 ms with the indexes present.
