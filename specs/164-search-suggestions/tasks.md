---
description: "Task list for Search suggestions while typing"
---

# Tasks: Search suggestions while typing

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [x] T001 `GET /api/products/suggest` - products through the listing's search, categories folded in memory; cached
- [x] T002 `SearchSuggestionTests` on PostgreSQL; a mutation

## Client

- [x] T003 Service, hook (debounced, keyed by the term)
- [x] T004 The search box as a combobox: suggestions, keyboard, Enter still searches
- [x] T005 Client tests; locales

## Contract, docs

- [x] T006 Bruno for the read and its refusal; `docs/reference` regenerated
- [x] T007 On the stack: the quickstart and a browser check
- [x] T008 Docs: catalog page, CLAUDE.md, timeline, backlog
- [x] T009 Merged in #378, closes #376

## Evidence

Verified on 2026-10-09 against the compose stack with Catalog and the storefront rebuilt from this branch (merged with
main after #377):

- **Server tests**: `Ecommerce.Catalog.Tests` 359 passed, 0 failed, on PostgreSQL - 8 in `SearchSuggestionTests` (a term
  without diacritics finds products and categories with them; nothing off the shelf and every category the list shows;
  at most 6 and 4; under two characters refused; the fold on `Máy ảnh`, `Đồng hồ`, `SÁCH`).
- **Mutation**: `Fold` keeping the marks fails 4 of the 8 (the three fold cases and the diacritics test). Restored.
- **Client**: 148 files, 819 tests passed - 6 for the search box (products and categories once typing pauses, nothing
  for one character, the keys open the highlighted option, a category opens the filtered listing, Enter with nothing
  highlighted searches and closes, Escape). Both apps type-check; lint clean.
- **Quickstart**: "may anh" suggests Máy ảnh, Máy ảnh compact, Máy ảnh không gương lật (Cameras, Compact cameras,
  Mirrorless cameras in English) and no product - the listing's search finds none either; "sony" 4 products priced in
  dong; "a" 400.
- **In a browser**: typing "sony" made one request and showed 4 options; Down, Down, Enter opened the A7C II; "may anh"
  in English showed the three camera categories; Enter searched `/?q=may%20anh` - and the dropdown stayed open over the
  results, which is how the "Enter closes it" fix and its test came about.
- **Found on the way**: the first version suggested only `IsActive` categories and on the seeded stack suggested none -
  the flag is set and read by nothing (research D6). Making it mean something is left for its own change.
- **Bruno**: 2 new requests in `product/` (seq 94-95); the whole collection 464/464 requests, 742/742 tests.
- **Docs**: `docs/reference` regenerated (228 endpoints).
