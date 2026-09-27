---
description: "Task list for Filter the catalogue by price and by what is in stock"
---

# Tasks: Filter the catalogue by price and by what is in stock

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Catalog

- [x] T001 [US1] [US2] `GetProductsQuery` fields and validator; `ProductFilter` through the repository
- [x] T002 [US1] Price bounds: default currency on `products.Price`; other currencies through the grouped join; the two indexes and the migration
- [x] T003 [US2] `inStock` on `products.Availability`
- [x] T004 `CatalogueFilterTests`: bounds in both currencies, the inactive variant, no price in the currency, in stock, validation, the plan in both currencies

## Phase 2: Storefront

- [x] T005 The price range and the in-stock switch on the catalogue, in the URL; words en/vi; tests

## Phase 3: Verification and docs

- [x] T006 Mutations (quickstart Scenario 3), each red; the full Catalog and client suites
- [x] T007 [P] Bruno; a rebuilt Catalog and storefront; the post-design Constitution re-check
- [x] T008 Docs: catalog, shopping-and-checkout, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T009 Merged as #229, closing #216

## Evidence

- Catalog 257/257, `CatalogueFilterTests` 7/7 with `SearchIndexTests` still green; client 557/557 (the catalogue page's
  first five tests), `oxlint` and `tsc -b` clean.
- The plan test found a plain `IX_products_Price` unused - the planner took `IX_products_ReviewStatus_SubmittedAt` and
  filtered the price - so the index is partial over the shelf predicate (`IX_products_on_shelf_Price`), and is used.
- Catalog mutations, each red then restored: the maximum ignored; the minimum exclusive; another currency reading the
  dong price; an inactive variant counting; in stock ignored; the per-product correlated MIN back; a reversed range
  allowed.
- Client mutations, each red: in stock not sent; the maximum not read from the address; a non-number bound sent; a
  reversed range sent; the range not applied; the switch inert.
- Bruno against a rebuilt Catalog and storefront: 348/348 requests, 561/561 tests; on the dev catalogue the dong range
  lists 21, the dollar one 24 and in stock 34 - not empty pages.
- Post-design Constitution re-check: see [plan.md](plan.md) - no violation found.
