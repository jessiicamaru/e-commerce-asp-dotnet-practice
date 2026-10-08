---
description: "Task list for The catalogue's public reads, served from memory"
---

# Tasks: The catalogue's public reads, served from memory

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [ ] T001 Baseline: `loadtest/run.sh browse` several warm runs on the stack before the change
- [ ] T002 `ICatalogueReadCache` (Application); `CatalogueWrites` interceptor (Infrastructure) registered on `CatalogDbContext`
- [ ] T003 `CatalogueCache` (WebApi): the `catalogue` policy - anonymous 200s, vary by query, language, currency, tag,
  expiry - the setting checked at start, eviction through the output cache store
- [ ] T004 `[OutputCache]` on the listing, a product, categories, a shop
- [ ] T005 `CatalogueCacheTests`: second read from memory; language/currency/query apart; signed-in bypass; non-200;
  eviction; switched off
- [ ] T006 `CatalogueWritesTests` on PostgreSQL: committed EF write, committed raw SQL, rollback, a view, a write
  outside a transaction; a mutation shown failing
- [ ] T007 After: rebuilt Catalog, several warm browse runs; a write shows at once on the stack
- [ ] T008 Docs: catalog page, load-test results, CLAUDE.md, timeline, backlog
- [ ] T009 Merged, closes #361

## Evidence

(Filled in when the work is verified.)
