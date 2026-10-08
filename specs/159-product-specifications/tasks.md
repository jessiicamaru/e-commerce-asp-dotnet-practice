---
description: "Task list for Product specifications per category"
---

# Tasks: Product specifications per category

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [ ] T001 Entities, configurations and the `AddProductSpecifications` migration (expand-only)
- [ ] T002 Declaring: create, rename, translate, delete specifications and options; the public list per category
- [ ] T003 Filling in: `PUT /api/products/{id}/specifications` - rules, ownership, review, audit
- [ ] T004 Reading: the lookup's `specifications`; the listing's `optionIds`
- [ ] T005 `CatalogueWrites` and the personal-data inventory know the new tables; the list is cached
- [ ] T006 `ProductSpecificationTests` on PostgreSQL; a mutation shown failing

## Client

- [ ] T007 Services and hooks; locale strings (incl. audit labels)
- [ ] T008 Storefront: the table on the product page; choice filters for the chosen category
- [ ] T009 Seller: the editor on the product page
- [ ] T010 Back office: specifications per category
- [ ] T011 Client tests

## Seed, contract, docs

- [ ] T012 Seed: specifications per vertical, values per product, checked; the seeder writes them
- [ ] T013 Bruno for every endpoint and refusal; `docs/reference` regenerated
- [ ] T014 On the stack: seeded twice, the table, the filter
- [ ] T015 Docs: catalog page, microservices design, CLAUDE.md, timeline, backlog
- [ ] T016 Merged, closes #366

## Evidence

(Filled in when the work is verified.)
