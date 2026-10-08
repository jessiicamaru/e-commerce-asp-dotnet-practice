---
description: "Task list for Product specifications per category"
---

# Tasks: Product specifications per category

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [x] T001 Entities, configurations and the `AddProductSpecifications` migration (expand-only)
- [x] T002 Declaring: create, rename, translate, delete specifications and options; the public list per category
- [x] T003 Filling in: `PUT /api/products/{id}/specifications` - rules, ownership, review, audit
- [x] T004 Reading: the lookup's `specifications`; the listing's `optionIds`
- [x] T005 `CatalogueWrites` and the personal-data inventory know the new tables; the list is cached
- [x] T006 `ProductSpecificationTests` on PostgreSQL; a mutation shown failing

## Client

- [x] T007 Services and hooks; locale strings (incl. audit labels)
- [x] T008 Storefront: the table on the product page; choice filters for the chosen category
- [x] T009 Seller: the editor on the product page
- [x] T010 Back office: specifications per category
- [x] T011 Client tests

## Seed, contract, docs

- [x] T012 Seed: specifications per vertical, values per product, checked; the seeder writes them
- [x] T013 Bruno for every endpoint and refusal; `docs/reference` regenerated
- [x] T014 On the stack: seeded twice, the table, the filter
- [x] T015 Docs: catalog page, microservices design, CLAUDE.md, timeline, backlog
- [ ] T016 Merged, closes #366

## Evidence

Verified on 2026-10-08 against the compose stack rebuilt from this branch:

- **Server tests**: `Ecommerce.Catalog.Tests` 317 passed, 0 failed, on PostgreSQL and S3 (8 in `ProductSpecificationTests`,
  2 new cases in `CatalogueWritesTests` for the new tables).
- **Mutation**: with `ProductReview.AfterSellerEditAsync` taken out of `SetProductSpecificationsCommandHandler`, the
  test that a seller's change sends an approved product back to review fails; with it, it passes.
- **Client**: 141 files, 774 tests passed. The suite found a real defect first: `createSpecification` sent the form's
  `english`/`optionsEnglish` fields to the server; they are now taken off before sending.
- **Seed**: `python seed/catalogue.py` checks the six files (specifications declared on 11 categories, values on 40
  products); `seed-catalogue.py` run twice - the second run declared nothing new and sent the same sets.
- **On the stack**: the Sony A7 IV reads Brand Sony / Sensor Full-frame / Resolution 33 MP in English and
  Thương hiệu Sony / Cảm biến Full-frame in Vietnamese; the T-shirt's material reads Cotton / Bông; Electronics
  filtered by Apple lists 3 products (iPhone 16, MacBook Air, AirPods Pro 2 - the spec first said 4, corrected); in
  the browser, Cameras filtered by Canon lists 3 and the R50's page shows its table.
- **Bruno**: the whole collection, 692 of 693 tests; the one failure is `my-data/payment`, already on `main` and
  filed as #364. The new `specifications` folder (14 requests) passes - its translate request first failed because it
  read the answer in the default language; it now asks with `?lang=en`.
- **Docs**: `docs/reference` regenerated (217 endpoints, 64 tables).
