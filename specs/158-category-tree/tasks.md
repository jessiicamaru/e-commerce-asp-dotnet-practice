---
description: "Task list for Categories in a tree - departments and their categories"
---

# Tasks: Categories in a tree - departments and their categories

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

## Server

- [x] T001 `CategoryTree` rules; `CreateCategory` checks the parent; `MoveCategoryCommand` + `PUT {id}/parent`, audited
- [x] T002 `DeleteCategory`: 409 with subcategories
- [x] T003 Listing filter includes subcategories
- [x] T004 `CategoryTreeTests` against PostgreSQL; a mutation shown failing

## Client

- [x] T005 `categoryTree` util; `Category.move`; locale strings (catalog, admin incl. the `CategoryMoved` audit label)
- [x] T006 Storefront: grouped filter, departments in the hero, breadcrumb on the product page
- [x] T007 Back office: tree, "Department" on create, "Move to" per row
- [x] T008 Client tests

## Seed, contract, docs

- [x] T009 Seed: `parent` in the format, checked; departments per vertical; the seeder creates and moves
- [x] T010 Bruno: move, its 400/403/404; `docs/reference` regenerated
- [x] T011 On the stack: seeded twice, the filter, the hero, a breadcrumb, a move from the back office
- [x] T012 Docs: catalog page, CLAUDE.md, timeline, backlog
- [ ] T013 Merged, closes #363

## Evidence

Verified 2026-10-08 on the compose stack (Catalog, storefront and back office rebuilt from this branch).

- **Server**: `CategoryTreeTests`, 8 tests, pass; the whole Catalog suite, 307, passes on this branch rebased onto the
  read cache (#362). Narrowing the listing's filter back to the category alone fails
  `A_department_lists_what_is_under_its_categories_and_a_category_only_its_own`; restored, it passes.
- **Client**: 760 tests in 137 files pass (the tree util, the hero's departments, the breadcrumb, the back office's tree,
  create-under and move, the `CategoryMoved` audit label); lint and both builds clean.
- **Seed checker**: an unknown parent, a third level and a category its own parent are each caught and named; the real
  files pass (6 verticals, 19 categories).
- **Seeding the stack seeded by #360**: the 6 departments created and the 13 categories moved under them; a second run
  sent nothing for categories. `GET /api/categories`: 19 rows, 6 departments (Books, Cameras, Electronics, Fashion,
  Home & living, Sports & outdoors). Filtering by Electronics lists 7 products (phones, laptops, headphones); by Phones, 3.
- **Storefront in the browser**: the hero's chips are the six departments; the filter lists each department with its
  categories indented and the department as hint; the iPhone 16's page reads "Electronics › Phones", each a link to the
  filtered listing.
- **Bruno, whole collection**: the seven new category requests pass (created under the department; a third level 400; the
  department 409; a customer 403; an unknown category 404; moved to the top; deleted) and the teardown still deletes the
  run's category. 2 of 679 tests fail, both already failing on `main` and filed as #364.
- **Not checked by hand**: the back office's tree in a browser - signing in as staff needs the second factor; its
  behaviour is covered by the page's tests.
