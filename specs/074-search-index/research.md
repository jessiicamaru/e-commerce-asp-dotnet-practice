# Research: Search uses an index

> Written on 2026-09-27, after the feature merged (#158), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-26 | **Issue**: #113

Seven decisions. D1 to D3 were what the issue asked for; D4 and D5 were found while building, and are the two
traps now recorded in CLAUDE.md; D6 and D7 are about proving it and shipping it safely. Where the record names no
rejected alternative, this says "not recorded" rather than supplying one.

---

## D1 - Make `unaccent` indexable with an IMMUTABLE wrapper that names its dictionary

**Decision**: A SQL function `f_unaccent(text) RETURNS text LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT AS
$$ SELECT unaccent('unaccent'::regdictionary, $1) $$`, mapped in EF as `SearchFunctions.Unaccent` with
`HasDbFunction(...).HasName("f_unaccent")`.

**Rationale**: PostgreSQL builds an index on an expression only if every function in it is IMMUTABLE, and
`unaccent(text)` is only STABLE - it looks its dictionary up through the search path at call time, so the same
input could give a different answer. Naming the dictionary explicitly removes that dependency, which is what makes
declaring the wrapper IMMUTABLE true rather than a lie told to the planner. The PR calls it "the standard
pattern". The indexes are built over exactly `f_unaccent(lower("Name"))`, so the query must say exactly that for
the planner to match them - hence a mapped function rather than raw SQL.

**Alternatives considered**:

- **No index at all** - the specs/021 D5 status quo. Rejected by #113: "invisible at 20 products; the first thing
  to fix at a hundred thousand", and measured here at 452 ms for a narrow search on 100,000 products.
- **A generated, stored unaccented column**, which issue #113 offered as the other option. Why the wrapper was
  preferred is not recorded; the PR states only that the wrapper is the standard pattern.

---

## D2 - `pg_trgm` GIN indexes over the exact expressions searched

**Decision**: `CREATE EXTENSION IF NOT EXISTS pg_trgm`, then three GIN indexes with `gin_trgm_ops`:
`IX_products_name_search` on `products (f_unaccent(lower("Name")))`, `IX_products_sku_search` on
`products (lower("Sku"))` and `IX_product_translations_name_search` on
`product_translations (f_unaccent(lower("Name")))`.

**Rationale**: search is a substring match - "may anh" anywhere in the name - and a trigram index can serve
`LIKE '%term%'` because it indexes every three-character piece of the value rather than its prefix. One index per
arm of the search (name, SKU, translated name) lets the planner combine them.

**Alternatives considered**: none recorded beyond D1's. The issue asked for trigram GIN indexes specifically.

---

## D3 - Match with `LIKE`, not with `string.Contains`

**Decision**: `EF.Functions.Like(SearchFunctions.Unaccent(p.Name.ToLower()), SearchFunctions.Unaccent(pattern),
SearchFunctions.Escape)`, where `pattern` is `%` + the escaped, trimmed, lower-cased term + `%`. The same for the
translation's name; the SKU is `EF.Functions.Like(p.Sku.ToLower(), pattern, SearchFunctions.Escape)`.

**Rationale**: `string.Contains` over a function expression becomes `strpos(...) > 0` in Npgsql, and a trigram
index can serve `LIKE` but not `strpos`. Building the `%...%` pattern once in C# and unaccenting the whole pattern
in SQL keeps both sides of the comparison going through the same `f_unaccent`, so "máy ảnh" and "may anh" meet in
the middle as before.

**Alternatives considered**:

- **Keep `Contains`.** Rejected: `strpos` cannot use the index, which is the whole feature. One nuance the PR
  records from its mutation checks: `Contains` on the **bare** SKU column is translated by Npgsql to a `LIKE`
  anyway, so switching the SKU arm back to `Contains` is an equivalent mutation that no test can catch.

---

## D4 - Escape the term, and name the escape character (found while building)

**Decision**: `SearchFunctions.ContainsPattern` escapes the term's own `\`, `%` and `_` (in that order) with a
backslash, and every `LIKE` passes `SearchFunctions.Escape` (`"\\"`, one backslash) as its escape character.

**Rationale**: `strpos` treated every character literally, so moving to `LIKE` would have given `%` and `_` a
meaning they never had - "50%" would find "5000" too. FR-001 forbids changing results, so the term is escaped.
The second half was found by a failing test: **Npgsql writes `ESCAPE ''` when `EF.Functions.Like` names no
escape character**, which is "no escape at all", so the backslashes the pattern contained were matched literally
and "50%" found nothing. `A_term_with_percent_or_underscore_is_matched_literally` caught it; the escape is now
named in every call.

**Alternatives considered**:

- **Don't escape.** Rejected: a search for "50%" would match "50" followed by anything - a change of results.
- **Rely on the default escape character.** Rejected by evidence: Npgsql's default in this call is `ESCAPE ''`.

---

## D5 - Translations as a `UNION` of ids, not an `OR EXISTS` (found while building)

**Decision**: the matching ids are `products` filtered by name-or-SKU, `UNION`ed with `product_translations`
filtered by language and name, and the listing is `WHERE p.Id IN (those ids)`.

**Rationale**: with the three indexes in place, a narrow search still took **457 ms** on 100,000 products. The
OR's third arm was a correlated `EXISTS` over translations, which PostgreSQL runs as a hashed SubPlan per
product - and no index on `products` can serve an OR that contains one, so it scanned every product. As two
indexed queries whose ids are unioned, the plan is a BitmapOr, a bitmap scan, then primary-key lookups: **4 ms**
(the PR's figure; the final narrow page measured 1.2 ms, its count 4.2 ms).

**Alternatives considered**:

- **Keep the single `Where` with `p.Translations.Any(...)`** (the `OR EXISTS` shape). Rejected: measured above.
  It still returns correct results, which is why D6's plan test asserts `SubPlan` is absent.

---

## D6 - Prove it with the real plan, twice

**Decision**: (a) For the PR, `EXPLAIN ANALYZE` on a scratch database built with the real migrations and seeded
with 100,000 products and 50,000 Vietnamese translations, running exactly the SQL EF generates, taken from EF's
own log. (b) In the suite, `SearchIndexTests`: a `DbCommandInterceptor` catches the search's `SELECT count(*)`
command, runs `SET enable_seqscan = off`, asks for `EXPLAIN` of the same text with the same parameters, and
asserts `IX_products_name_search` and `IX_products_sku_search` appear, a `LIKE` over `f_unaccent` (`~~`) matches
translations, and there is no `Seq Scan`, no `SubPlan` and no `strpos`.

**Rationale**: every regression here returns the right rows, so no result-based test can see one, and on a test
database of a few rows the planner rightly prefers a sequential scan. Pricing sequential scans out asks what the
planner *could* do: a shape that cannot use the indexes still shows a scan or a subplan. Taking the SQL from EF
rather than writing it by hand is what makes the measurement about the code that runs (constitution V).

The PR's mutation checks: the name through the STABLE `unaccent`, no escape on the name, and the `OR EXISTS` shape
were all caught. The first version of the plan test let `OR EXISTS` through - with seq scans off it used another
index - and the `SubPlan` assertion was added for that. Two mutations are equivalent: the SKU through `Contains`
(D3) and the SKU arm's escape, which these terms never exercise.

**Alternatives considered**:

- **The first plan test without the `SubPlan` assertion.** Rejected by its own mutation check, as above.

---

## D7 - An additive migration, whose `Down` keeps the extensions

**Decision**: `AddSearchIndexes` only adds: `CREATE EXTENSION IF NOT EXISTS pg_trgm` and `unaccent`,
`CREATE OR REPLACE FUNCTION f_unaccent`, three `CREATE INDEX IF NOT EXISTS`. `Down` drops the three indexes and the
function, and leaves both extensions.

**Rationale**: the constitution's Schema-evolution rule - an index and a function are additive, and an earlier
image's query (`strpos` over `unaccent`) still runs against this schema, just without the indexes, so rolling
Catalog back does not take the catalogue down. The extensions stay on `Down` because "other things may use" them:
`unaccent` was created by `20260922052231_AddTranslations`, and dropping it would break the older search.

**Alternatives considered**: not recorded.
