---
description: "Task list for Search uses an index"
---

# Tasks: Search uses an index

> Completed on 2026-09-27, after the feature merged (#158), from the code at that merge, the pull request and docs/features/catalog.md.

**Input**: Design documents from `/specs/074-search-index/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The results are pinned by the existing search tests, and the index use by a plan test -
every regression here returns correct rows, so only a test on the plan can see it (constitution V).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 fast search with the same results, US2 literal `%` and `_`, US3 a regression fails a test

## The tasks as written at the time

Kept as they were. T004 onward break them into the pieces the merged change actually contains, including what
was found while building.

- [X] T001 Test: a term containing `%` or `_` matches literally. The existing diacritic tests stay unchanged.
- [X] T002 Migration `AddSearchIndexes`: `pg_trgm`, `f_unaccent`, and three GIN indexes. A `HasDbFunction` mapping for `f_unaccent`. The query changes to `LIKE` with escaping.
- [X] T003 `EXPLAIN ANALYZE` before and after on 100k products in a scratch database. Docs: the catalogue page, CLAUDE.md, the timeline and the backlog.

---

## Phase 1: Tests first

- [X] T004 [P] [US2] Add `A_term_with_percent_or_underscore_is_matched_literally` to `server/tests/Ecommerce.Catalog.Tests/TranslationTests.cs`: "Sale 50%" vs "Sale 5000", "Lens a_b" vs "Lens axb"; leave `Searching_ignores_diacritics_and_looks_at_both_the_translation_and_the_original` untouched (the definition of "the same results", FR-001) - part of T001

## Phase 2: Schema (the migration)

- [X] T005 [US1] Create `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260926092756_AddSearchIndexes.cs`: `CREATE EXTENSION IF NOT EXISTS pg_trgm` and `unaccent`; `CREATE OR REPLACE FUNCTION f_unaccent(text)` `IMMUTABLE PARALLEL SAFE STRICT` over `unaccent('unaccent'::regdictionary, $1)` (research D1) - part of T002
- [X] T006 [US1] In the same migration, the three GIN `gin_trgm_ops` indexes `IX_products_name_search`, `IX_products_sku_search`, `IX_product_translations_name_search`, all `IF NOT EXISTS` (research D2); `Down` drops them and the function and keeps both extensions (research D7) - part of T002

## Phase 3: The query

- [X] T007 [P] [US1] Create `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/SearchFunctions.cs`: `Unaccent` (translated only; throws in .NET), `Escape`, and `ContainsPattern` escaping `\`, `%`, `_` and wrapping in `%...%` - part of T002
- [X] T008 [US1] Map it in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/CatalogDbContext.cs`: `HasDbFunction(...SearchFunctions.Unaccent).HasName("f_unaccent")` - part of T002
- [X] T009 [US1] [US2] Rewrite the search in `ProductRepository.GetPaginatedAsync` (`server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs`) as `EF.Functions.Like(SearchFunctions.Unaccent(...), SearchFunctions.Unaccent(pattern), SearchFunctions.Escape)` for names and `EF.Functions.Like(p.Sku.ToLower(), pattern, SearchFunctions.Escape)` for the SKU (research D3) - part of T002
- [X] T010 [US2] Found while building: name the escape character in every `LIKE` - Npgsql writes `ESCAPE ''` otherwise and "50%" found nothing; T004 is what failed (research D4)
- [X] T011 [US1] Found while building: replace the `OR ... p.Translations.Any(...)` arm with a `UNION` of matching ids and `WHERE p.Id IN (...)` - the `OR EXISTS` shape kept every product scanned, 457 ms with the indexes present, 4 ms as a UNION (research D5)

## Phase 4: Evidence

- [X] T012 [US3] Create `server/tests/Ecommerce.Catalog.Tests/SearchIndexTests.cs`: a `DbCommandInterceptor` explains the search's count query with `enable_seqscan = off` and asserts both `products` indexes, a `LIKE` over `f_unaccent`, no `Seq Scan`, no `SubPlan`, no `strpos` (research D6)
- [X] T013 [US3] Mutation-check `SearchIndexTests`: the STABLE `unaccent` on the name, no escape on the name and the `OR EXISTS` shape are caught; the first version missed `OR EXISTS` and gained the `SubPlan` assertion; two equivalent mutations recorded (SKU through `Contains`, the SKU arm's escape)
- [X] T014 [US1] `EXPLAIN ANALYZE` of the SQL from EF's log on a scratch database with the real migrations, 100,000 products and 50,000 translations: 452 ms to 1.2 ms narrow, 426 ms to 81 ms broad, 516 ms to 4.2 ms count - recorded in the PR - part of T003
- [X] T015 Run the Catalog suite (169/169), rebuild the Catalog container (the migration applied at startup; the three indexes exist; `GET /api/products?searchTerm=may anh` answers), and run Bruno through the storefront (232/232, 379/379)

## Phase 5: Documentation - part of T003

- [X] T016 [P] `docs/features/catalog.md`: the "Search and sort" paragraph, rule 15 rewritten with the measurement, the "Search scans every product" known limit removed
- [X] T017 [P] `CLAUDE.md` (search is indexed, both traps, the Catalog test count), `docs/project/timeline.md` (row 074), `docs/project/backlog.md` (#113 moved to closed), `docs/testing/testing-strategy.md` and `docs/overview/project-overview.md` (counts)
- [X] T018 Merge: PR [#158](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/158), "refactor(catalog): search uses trigram indexes - 452 ms to 1.2 ms on 100,000 products", merged 2026-09-26, closing #113

---

## Dependencies & Execution Order

- T004 before T009/T010: the literal-match test is what exposed Npgsql's `ESCAPE ''`.
- T005-T006 before T009: the query must name the function the indexes are built over.
- T007-T008 before T009.
- T011 was a consequence of T014's first measurement; T012 then pins it, and T013 is what made T012 able to.
- T016-T017 alongside, T018 last.

## Notes

- The seeding script for T014 was not committed; quickstart scenario 7 describes the measurement.
- Not done here, recorded in [plan.md](./plan.md): short terms and index write cost were not measured, and the
  docs text of `bruno/product/search without diacritics.yml` still says the search has no index.
