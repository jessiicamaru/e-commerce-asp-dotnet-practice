---
description: "Task list for Search suggestions while typing"
---

# Tasks: Search suggestions while typing

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [ ] T001 `GET /api/products/suggest` - products through the listing's search, categories folded in memory; cached
- [ ] T002 `SearchSuggestionTests` on PostgreSQL; a mutation

## Client

- [ ] T003 Service, hook (debounced, keyed by the term)
- [ ] T004 The search box as a combobox: suggestions, keyboard, Enter still searches
- [ ] T005 Client tests; locales

## Contract, docs

- [ ] T006 Bruno for the read and its refusal; `docs/reference` regenerated
- [ ] T007 On the stack: the quickstart and a browser check
- [ ] T008 Docs: catalog page, CLAUDE.md, timeline, backlog
- [ ] T009 Merged, closes #376

## Evidence

(Filled in when the work is verified.)
