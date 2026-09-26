# Tasks: Search uses an index

- [ ] T001 Test: a term containing `%` or `_` matches literally. The existing diacritic tests stay unchanged.
- [ ] T002 Migration `AddSearchIndexes`: `pg_trgm`, `f_unaccent`, and three GIN indexes. A `HasDbFunction` mapping for `f_unaccent`. The query changes to `LIKE` with escaping.
- [ ] T003 `EXPLAIN ANALYZE` before and after on 100k products in a scratch database. Docs: the catalogue page, CLAUDE.md, the timeline and the backlog.
