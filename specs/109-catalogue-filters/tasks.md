---
description: "Task list for Filter the catalogue by price and by what is in stock"
---

# Tasks: Filter the catalogue by price and by what is in stock

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Catalog

- [ ] T001 [US1] [US2] `GetProductsQuery` fields and validator; `ProductFilter` through the repository
- [ ] T002 [US1] Price bounds: default currency on `products.Price`; other currencies through the grouped join; the two indexes and the migration
- [ ] T003 [US2] `inStock` on `products.Availability`
- [ ] T004 `CatalogueFilterTests`: bounds in both currencies, the inactive variant, no price in the currency, in stock, validation, the plan in both currencies

## Phase 2: Storefront

- [ ] T005 The price range and the in-stock switch on the catalogue, in the URL; words en/vi; tests

## Phase 3: Verification and docs

- [ ] T006 Mutations (quickstart Scenario 3), each red; the full Catalog and client suites
- [ ] T007 [P] Bruno; a rebuilt Catalog and storefront; the post-design Constitution re-check
- [ ] T008 Docs: catalog, shopping-and-checkout, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T009 Merged as #229, closing #216
