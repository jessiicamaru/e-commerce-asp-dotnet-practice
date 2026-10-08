---
description: "Task list for The catalogue's public reads, served from memory"
---

# Tasks: The catalogue's public reads, served from memory

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [x] T001 Baseline: `loadtest/run.sh browse` several warm runs on the stack before the change
- [x] T002 `ICatalogueReadCache` (Application); `CatalogueWrites` interceptor (Infrastructure) registered on `CatalogDbContext`
- [x] T003 `CatalogueCache` (WebApi): the `catalogue` policy - anonymous 200s, vary by query, language, currency, tag,
  expiry - the setting checked at start, eviction through the output cache store
- [x] T004 `[OutputCache]` on the listing, a product, categories, a shop
- [x] T005 `CatalogueCacheTests`: second read from memory; language/currency/query apart; signed-in bypass; non-200;
  eviction; switched off
- [x] T006 `CatalogueWritesTests` on PostgreSQL: committed EF write, committed raw SQL, rollback, a view, a write
  outside a transaction; a mutation shown failing
- [x] T007 After: rebuilt Catalog, several warm browse runs; a write shows at once on the stack
- [x] T008 Docs: catalog page, load-test results, CLAUDE.md, timeline, backlog
- [ ] T009 Merged, closes #361

## Evidence

Verified 2026-10-08.

- **Tests**: `CatalogueCacheTests` and `CatalogueWritesTests`, 31 tests, pass; the whole Catalog suite, 299, passes.
- **Mutations**: evicting at the statement instead of after the commit fails `A_rolled_back_write_empties_nothing` and
  `A_guarded_statement_in_a_transaction_empties_it_after_the_commit_not_before`; leaving the language out of the key
  fails `Another_language_currency_or_query_is_another_answer`. Both restored.
- **On the stack** (Catalog rebuilt from this branch): the category list read anonymously took 405 ms cold and 22 ms
  the second time; renamed by the administrator, the very next anonymous read showed the new name; renamed back.
- **Load** (`loadtest/run.sh browse`, peak 50, 41 products, same stack; 4 warm runs each way, the first run after the
  rebuild kept as a warm-up and not compared). Milliseconds, median / p95:

  | | list products | search | one product | unexpected |
  | :-- | --: | --: | --: | --: |
  | without the cache | 10.1-20.1 / 38.2-78.1 | 10.1-20.4 / 35.4-58.8 | 7.6-12.7 / 22.1-36.8 | 0 |
  | with it | 2.4-3.0 / 4.7-6.0 | 2.3-2.9 / 4.5-5.9 | 2.2-2.8 / 4.6-6.0 | 0 |

  Summaries in `server/loadtest/results/browse-2026-10-08T08-*.json`; the table in `docs/testing/load-test-results.md`
  is generated from them (`loadtest/report.py`, which now names the runs from before the cache). The scenario repeats a
  small set of questions, so almost every request is a hit: the best case, stated as such in the evaluation.
