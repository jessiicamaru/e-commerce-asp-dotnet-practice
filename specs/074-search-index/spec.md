# Feature Specification: Search uses an index

> Completed on 2026-09-27, after the feature merged (#158), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature Branch**: `074-search-index` | **Created**: 2026-09-26 | **Issue**: #113 (closes it)

**Status**: Merged - PR [#158](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/158), 2026-09-26

**Input**: Issue #113, "refactor(catalog): search scans every product" - "Diacritic-insensitive search (specs/021)
calls `unaccent()` on every row, so it cannot use an index - a sequential scan of products and translations on
every search. Invisible at 20 products; the first thing to fix at a hundred thousand." It asked for an IMMUTABLE
wrapper around `unaccent` and a trigram GIN index (or a generated, stored unaccented column), and for
`EXPLAIN ANALYZE` before and after on a generated catalogue of 100k products, recorded in the PR.

## Why

Diacritic-insensitive search (specs/021) calls `unaccent()` on every row and matches with `strpos`. `unaccent()`
is only STABLE, so PostgreSQL cannot index an expression that uses it, and a search therefore scans every
product and every translation. That is invisible at 50 products and the first thing to fix at 100,000.

The cost was known and recorded when search was built: [specs/021 research D5](../021-internationalisation/research.md)
chose `unaccent` with **no index**, because an index needs an IMMUTABLE wrapper function and that was "a
deliberate piece of DBA work rather than something to slip into a feature migration". This feature is that work.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper's search stays fast on a large catalogue (Priority: P1)

A shopper types a word into the search box - with or without Vietnamese diacritics - and gets the same page of
results they got before, without the shop reading every product it holds to find them.

**Why this priority**: This is the whole of issue #113. Search is on the storefront's landing page and in the
seller's own product list, and its cost grew with every product listed. Nothing else in this feature matters
if the query still scans.

**Independent Test**: On a catalogue of 100,000 products, run `EXPLAIN ANALYZE` on the SQL EF generates for a
search before and after the migration; the "after" plan uses the new indexes and no sequential scan of
`products`. Separately, the existing search tests pass unchanged, which is what "the same results" means.

**Acceptance Scenarios**:

1. **Given** a product named "Máy ảnh ...", **When** a shopper searches "may anh" in Vietnamese, **Then** it is
   found, exactly as before this feature.
2. **Given** a product whose Vietnamese translation matches but whose original name does not, **When** a shopper
   searches in Vietnamese, **Then** it is found, and a product found only by its original name is found too.
3. **Given** a catalogue of 100,000 products, **When** a narrow search runs, **Then** the plan uses the trigram
   indexes on names and SKUs and on translations, and the time recorded in the PR falls from 452 ms to 1.2 ms.

---

### User Story 2 - A search for "50%" means those three characters (Priority: P2)

A shopper searches for a term that contains `%` or `_`. They get the products whose names contain those
characters, not every product that starts with "50" or has any one character where the `_` is.

**Why this priority**: Moving the match from `strpos` to `LIKE` - which the index needs - gives `%`, `_` and `\`
a meaning they did not have. Without escaping them, the index work would quietly change results, which FR-001
forbids. It is second because it only exists as a consequence of User Story 1.

**Independent Test**: Create "Sale 50% x", "Sale 5000 x", "Lens a_b x" and "Lens axb x"; a search for "50% x"
returns only the first and a search for "a_b x" only the third.

**Acceptance Scenarios**:

1. **Given** products "Sale 50% m" and "Sale 5000 m", **When** a shopper searches "50% m", **Then** only the first
   is returned.
2. **Given** products "Lens a_b m" and "Lens axb m", **When** a shopper searches "a_b m", **Then** only the first
   is returned.

---

### User Story 3 - A change that makes search scan again fails a test (Priority: P3)

A developer later rewrites the search - calls `unaccent` instead of `f_unaccent`, uses `Contains` again, or puts
the translations back into one `OR`. The results stay correct, so every result-based test passes; a plan-based
test fails instead.

**Why this priority**: every regression this feature guards against returns correct results and is only slower.
Nothing that asserts results can see it, and at test-database size nothing is slow. Without a test on the plan
the index would be one refactor from being bypassed with no one noticing. Third, because it protects the other
two rather than delivering anything by itself.

**Independent Test**: Run `SearchIndexTests`; then apply each mutation the PR lists (the STABLE `unaccent` on the
name, no escape on the name, the `OR EXISTS` shape) and see it fail.

**Acceptance Scenarios**:

1. **Given** the search as merged, **When** `SearchIndexTests` asks PostgreSQL for the plan of the count query EF
   generates with sequential scans priced out, **Then** the plan names `IX_products_name_search` and
   `IX_products_sku_search`, matches translations with a `LIKE` over `f_unaccent`, and contains no `Seq Scan`, no
   `SubPlan` and no `strpos`.
2. **Given** the translations matched through a correlated `EXISTS` inside one `OR`, **When** the test runs,
   **Then** it fails on the `SubPlan` assertion.

---

### Edge Cases

- **An empty or whitespace term.** No filter is applied at all, as before (`string.IsNullOrWhiteSpace`).
- **A term with a backslash.** The backslash is escaped too, so it matches a literal backslash.
- **A broad term.** "may anh" matching 10,000 of 100,000 products still reads that many rows; the PR measured
  426 ms before and 81 ms after. The index narrows the candidates; it cannot make a broad match cheap.
- **A term shorter than three characters.** Trigrams are three characters; how the plan behaves for one- or
  two-character terms was not measured - not recorded.
- **The SKU arm.** The product's `Sku` is matched lower-cased but **not** unaccented, as it was before; SKUs are
  ASCII in practice. Variant SKUs (`product_variants.Sku`) are not searched, before or after.
- **Other languages' translations.** Only the requested language's translation is searched, plus the original
  name - unchanged from specs/021.
- **A product not on sale.** The public listing still filters to `ReviewStatus = 'Approved'` (specs/045); the
  index finds candidate ids, the rest of the query decides what is listed.
- **A seller's own page.** `GET /api/products/mine` runs the same repository method with `listedOnly: false`, so it
  gets the same index and the same escaping.

## Requirements *(mandatory)*

### Functional Requirements

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
- **FR-006** *(found while building)* The translations MUST NOT be matched in a way that forces a scan of
  `products`: the ids matched by name or SKU and the ids matched by translation are combined with a `UNION`, not
  with an `OR` containing a correlated `EXISTS`.
- **FR-007** *(found while building)* The `LIKE` MUST name its escape character. Npgsql writes `ESCAPE ''` - no
  escape at all - when `EF.Functions.Like` is given none.
- **FR-008** The shape of the query MUST be pinned by an automated test that reads its plan, because every
  regression it guards against returns correct results.
- **FR-009** The migration MUST be additive: an earlier Catalog image's query must still run against the new
  schema.

> **Corrected on 2026-09-27**: FR-004 describes the SQL loosely. In the code the pattern is built in C#, not in
> SQL: the term is trimmed and lower-cased, its `%`, `_` and `\` are escaped, it is wrapped in `%...%`
> (`SearchFunctions.ContainsPattern`), and the whole pattern is then passed through `f_unaccent` in the query -
> `f_unaccent(lower("Name")) LIKE f_unaccent(@pattern) ESCAPE '\'`. The SKU arm is
> `lower("Sku") LIKE @pattern ESCAPE '\'`, with no `f_unaccent` on either side. FR-001's "diacritics ignored on
> both sides" applies to the name and the translation; the SKU was never unaccented, before or after.

### Key Entities

- **`f_unaccent(text)`**: a SQL function in the Catalog database. It is `unaccent` with the dictionary named
  (`'unaccent'::regdictionary`), declared `IMMUTABLE PARALLEL SAFE STRICT`, and mapped in EF as
  `SearchFunctions.Unaccent`.
- **The three search indexes**: `IX_products_name_search`, `IX_products_sku_search` and
  `IX_product_translations_name_search`, GIN with `gin_trgm_ops`, over exactly the expressions the query uses.

No table, column or response changed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

All figures are the PR's, measured on a scratch database built with the real migrations and seeded with
100,000 products (names in Vietnamese and English with diacritics) and 50,000 Vietnamese translations, using the
SQL taken from EF's own log.

- **SC-001**: A narrow search (one match, a page of 12) falls from **452 ms** (sequential scans of products and
  translations) to **1.2 ms** (a BitmapOr over the name and SKU indexes plus the translations index).
- **SC-002**: A broad search ("may anh", 10,000 matches) falls from **426 ms** to **81 ms**.
- **SC-003**: The narrow search's count query falls from **516 ms** to **4.2 ms**.
- **SC-004**: Every existing search test passes unchanged - Catalog 169/169 at merge, two of them new.
- **SC-005**: A search for "50%" or "a_b" returns only the products containing those exact characters.
- **SC-006**: `SearchIndexTests` fails for each of the three non-equivalent mutations the PR lists.
- **SC-007**: The Bruno collection passes through the storefront - 232/232 requests, 379/379 assertions.

## Acceptance

- The existing search tests still pass.
- A new test: a term containing `%` or `_` is matched literally.
- The plan shows a Bitmap Index Scan on the new indexes.

> Note, 2026-09-27: the automated plan test (`SearchIndexTests`) asserts the two `products` indexes by name. On
> the test database's tiny translations table the planner reaches translations through the unique
> `(ProductId, Language)` index and filters by name, so the test asserts a `LIKE` over `f_unaccent` there rather
> than `IX_product_translations_name_search`. The PR reports the translations index in use on 100,000 rows.

## Assumptions

- The PostgreSQL image (`postgres:16-alpine`) ships the `unaccent` and `pg_trgm` contrib extensions;
  `unaccent` was already created by `20260922052231_AddTranslations` (specs/021).
- Results are defined by the existing tests. No ranking by relevance is expected: the order stays the requested
  sort (name, or price in the requested currency).
- A catalogue of 100,000 products is the size the issue named; nothing larger was measured.
- The extra write cost of three GIN indexes on product and translation writes is acceptable at this shop's write
  rate. It was not measured - not recorded.

## Out of Scope

- Searching descriptions, variant SKUs or option text.
- Sorting by the translated name (sorting stays on the default-language `Name`).
- Relevance ranking, typo tolerance or `similarity()` thresholds - the trigram index is used only to serve `LIKE`.
- Full-text search (`tsvector`), a search engine, or a generated unaccented column.
- Any change to the HTTP request or response.
