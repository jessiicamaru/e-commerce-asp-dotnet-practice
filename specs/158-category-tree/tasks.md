---
description: "Task list for Categories in a tree - departments and their categories"
---

# Tasks: Categories in a tree - departments and their categories

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [ ] T001 `CategoryTree` rules; `CreateCategory` checks the parent; `MoveCategoryCommand` + `PUT {id}/parent`, audited
- [ ] T002 `DeleteCategory`: 409 with subcategories
- [ ] T003 Listing filter includes subcategories
- [ ] T004 `CategoryTreeTests` against PostgreSQL; a mutation shown failing

## Client

- [ ] T005 `categoryTree` util; `Category.move`; locale strings (catalog, admin incl. the `CategoryMoved` audit label)
- [ ] T006 Storefront: grouped filter, departments in the hero, breadcrumb on the product page
- [ ] T007 Back office: tree, "Department" on create, "Move to" per row
- [ ] T008 Client tests

## Seed, contract, docs

- [ ] T009 Seed: `parent` in the format, checked; departments per vertical; the seeder creates and moves
- [ ] T010 Bruno: move, its 400/403/404; `docs/reference` regenerated
- [ ] T011 On the stack: seeded twice, the filter, the hero, a breadcrumb, a move from the back office
- [ ] T012 Docs: catalog page, CLAUDE.md, timeline, backlog
- [ ] T013 Merged, closes #363

## Evidence

(Filled in when the work is verified.)
