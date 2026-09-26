# Implementation Plan: Search uses an index

> Written on 2026-09-27, after the feature merged (#158), from the code at that merge, the pull request and docs/features/catalog.md.

**Branch**: `074-search-index` | **Date**: 2026-09-26 | **Spec**: [spec.md](./spec.md) | **Issue**: #113

**Input**: Feature specification from `/specs/074-search-index/spec.md`

## Summary

Make the catalogue search - `GET /api/products?searchTerm=` and the seller's `GET /api/products/mine?searchTerm=` -
answerable from an index, with exactly the same results.

`unaccent()` is STABLE, so no index can be built on an expression that uses it. The `AddSearchIndexes` migration
adds `f_unaccent(text)`, an IMMUTABLE SQL wrapper that names `unaccent`'s dictionary, and three `pg_trgm` GIN
indexes over the exact expressions the query uses: `f_unaccent(lower("Name"))` on `products` and on
`product_translations`, and `lower("Sku")` on `products`. `ProductRepository.GetPaginatedAsync` changes in three
ways: the match is a `LIKE '%...%'` (which a trigram index serves; the `strpos` that `string.Contains` became
does not), the term's `%`, `_` and `\` are escaped with a named escape character, and the translations are
matched as a `UNION` of ids rather than an `OR EXISTS`.

Two things surfaced while building and are folded in (research D4, D5):

- **With the indexes in place, products were still scanned whole.** The OR's third arm was a correlated `EXISTS`
  over translations, which runs as a hashed SubPlan per product; no index on `products` can serve an OR that
  contains one. A narrow search still took 457 ms. Two indexed queries whose ids are UNIONed give a BitmapOr, a
  bitmap scan, then primary-key lookups: 4 ms.
- **Npgsql writes `ESCAPE ''` when `EF.Functions.Like` names no escape character.** That is no escape at all, so
  the pattern's backslashes were matched literally and "50%" found nothing. The new literal-match test caught it.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: EF Core with Npgsql (`EF.Functions.Like` with an escape argument, `HasDbFunction`),
PostgreSQL contrib extensions `pg_trgm` and `unaccent`

**Storage**: PostgreSQL 16 (`postgres:16-alpine`), `ecommerce_catalog_db` on host port 5433. No table or column
changed; one function and three indexes were added

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Catalog.Tests`, 169 tests at merge). The existing
search tests in `TranslationTests` pin the results; the new `SearchIndexTests` pins the plan through an EF
`DbCommandInterceptor`. Manual `EXPLAIN ANALYZE` on a 100,000-product scratch database for the timings

**Target Platform**: The Catalog service (REST on 5057), Linux container / Windows dev host

**Project Type**: A change inside one existing backend service (Infrastructure layer only) plus a migration

**Performance Goals**: The plan uses the indexes (FR-005). No latency target was set in advance; the measured
result is 452 ms to 1.2 ms for a narrow search on 100,000 products (spec SC-001 to SC-003)

**Constraints**: The same results as before (FR-001). The migration must be additive, so an earlier image still
runs (constitution, Schema evolution). A `%` or `_` in a term must keep meaning itself

**Scale/Scope**: Measured at 100,000 products and 50,000 translations; the running catalogue is the seeded
cameras plus whatever sellers list

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. This plan was written after
the merge, from the code; the design did not pass through a gate at the time, so this is the check it would have
had.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Catalog indexes its own tables in its own database. No other service, message or contract is involved; the product translations searched are Catalog's own rows. |
| **II. Clean Architecture Layering** | **Pass.** Everything is in Infrastructure: `SearchFunctions`, the `HasDbFunction` mapping in `CatalogDbContext`, the query in `ProductRepository` and the migration. `IProductRepository.GetPaginatedAsync`'s signature and the Application-layer queries (`GetProductsQuery`, `GetMyProductsQuery`) are unchanged; the controller still only dispatches. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass (not engaged).** The feature is read-only: no write, no publish, no consumer. The migration's DDL uses `IF NOT EXISTS` / `CREATE OR REPLACE`, so re-running it is harmless. |
| **IV. Identity Comes From the Token** | **Pass.** No endpoint's authorization changed: `GET /api/products` stays `[AllowAnonymous]` and `/mine` stays `Seller`, whose seller id still comes from `ICurrentUser`. The search term is data, not identity. |
| **V. Evidence Over Assumption** | **Pass, and central.** The timings come from `EXPLAIN ANALYZE` of the exact SQL EF generates (taken from EF's log) on 100,000 real rows, not from a hand-written query. The plan test runs against real PostgreSQL, because the property is the planner's - an in-memory provider has no plan. It was mutation-checked, and its first version let the `OR EXISTS` shape through, which is how the `SubPlan` assertion came to exist. |

**Post-design re-check**: no violations. The Schema-evolution rule is met: the migration adds two extensions
(one already present), one function and three indexes - all additive - and an earlier image's query (`strpos`
over `unaccent`) still runs against the new schema, just without the indexes.

## Project Structure

### Documentation (this feature)

```text
specs/074-search-index/
├── spec.md              # Feature specification (completed 2026-09-27)
├── plan.md              # This file
├── research.md          # Seven decisions with rejected alternatives
├── data-model.md        # The function, the extensions and the three indexes; nothing else changed
├── quickstart.md        # Validation scenarios
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── contracts/
│   └── http-api.md      # The search endpoints: request and response unchanged, behaviour and cost changed
└── tasks.md             # Task list (completed 2026-09-27)
```

### Source Code (repository root)

```text
server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/
├── Migrations/
│   ├── 20260926092756_AddSearchIndexes.cs           # pg_trgm, unaccent, f_unaccent, three GIN indexes
│   └── 20260926092756_AddSearchIndexes.Designer.cs
└── Persistence/
    ├── SearchFunctions.cs                           # Unaccent (maps to f_unaccent), Escape, ContainsPattern
    ├── CatalogDbContext.cs                          # HasDbFunction(...).HasName("f_unaccent")
    └── Repositories/ProductRepository.cs            # GetPaginatedAsync: escaped LIKE, UNION of ids

server/tests/Ecommerce.Catalog.Tests/
├── SearchIndexTests.cs                              # new: the plan uses the indexes, no Seq Scan, no SubPlan
└── TranslationTests.cs                              # new test: A_term_with_percent_or_underscore_is_matched_literally
```

Documentation touched in the same PR: `docs/features/catalog.md` (Search and sort paragraph, rule 15 rewritten,
the "Search scans every product" known limit removed), `CLAUDE.md`, `docs/project/timeline.md`,
`docs/project/backlog.md`, `docs/testing/testing-strategy.md`, `docs/overview/project-overview.md`.

**Structure Decision**: no new project, folder or layer. `SearchFunctions` sits beside `CatalogDbContext` in
`Persistence/` because it is a database-function mapping that only EF can call - its `Unaccent` body throws if
run in .NET.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **A broad search is faster, not cheap.** "may anh" matching 10,000 of 100,000 products went from 426 ms to
  81 ms; the index narrows candidates but the matching rows are still read and counted.
- **Short terms were not measured.** Trigram indexes work in three-character pieces; how one- and two-character
  terms plan was not recorded.
- **Sorting by name uses the default-language `Name`**, and search matches the product's SKU (not variant SKUs)
  with a plain `LIKE` and does not search descriptions - still listed under "Known limits" in
  [catalog.md](../../docs/features/catalog.md).
- **The write cost of the three GIN indexes** on product and translation writes was not measured.
- **The 100,000-product seeding used for the measurement is not in the repository**, so the timings cannot be
  reproduced from a script here; quickstart.md describes how to repeat the measurement by hand.
- **A stale sentence outside this feature**: the docs of `bruno/product/search without diacritics.yml` still say
  "No index: `unaccent()` is not IMMUTABLE, so this is a sequential scan" - true before this PR, not after it. The
  PR did not touch that file.
