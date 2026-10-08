# Research: Categories in a tree - departments and their categories

## D1. Two levels, enforced

**Decision**: a category is either a department (no parent) or under one; a department's subcategory cannot have its
own subcategories.

**Rationale**:
- The six verticals of the seed (specs/156) map onto it exactly: Điện tử › Điện thoại, Thời trang › Giày ...
- Every screen that shows the tree - a select, chips, a breadcrumb - stays a list with one indent, which is what a
  phone screen can show.
- A rule the server enforces is one the client can rely on: the filter and the breadcrumb never meet a third level.
- Filtering by a department is one indexed subquery (its children's ids), not a recursive CTE.

**Alternatives rejected**: unlimited depth (recursive queries for the filter, a tree widget for the select, a
breadcrumb of any length - for a shop with thirteen categories); a materialised path column (a migration and a rewrite
on every move, for depth this shop does not have).

## D2. Moving is its own endpoint

**Decision**: `PUT /api/categories/{id}/parent`, body `{ "parentCategoryId": guid | null }`.

**Rationale**: `PUT /api/categories/{id}` (specs/097) takes name and description. Adding the parent to it would make
every existing caller - the back office's rename, Bruno, the seeder - send `null` and silently lift the category out of
its department. A separate verb also gets its own audit action, `CategoryMoved`, which reads better in the log than a
rename that changed a parent.

## D3. Where the rules live

`CategoryTree` in Application: one method that, given the category, the proposed parent and whether the category has
subcategories, says why not - used by create and by move, so the two cannot drift. The database keeps the `RESTRICT`
foreign key as the last word on existence; the handler checks first so the caller gets a sentence (as specs/024 did for
products in a category).

## D4. The filter

`p.CategoryId == id || <categories whose ParentCategoryId == id>.Contains(p.CategoryId)` - one subquery on the parent's
index (EF creates `IX_categories_ParentCategoryId` for the foreign key). It composes with every other filter and with
the search's plan (specs/074, specs/109) unchanged.

## D5. The client

- `categoryTree(categories)` in core: departments sorted by name, each with its children sorted by name; a child whose
  parent is missing from the list is treated as a department, so a stale list never hides a category.
- Filter: one option per department and one indented option per child (`  ↳ Điện thoại` is not used - the label is
  the name, the indent is the option's style), because the searchable select is a flat list.
- Hero: the departments.
- Breadcrumb: from the product's `categoryId` and the category list already cached by the query layer - no new request.
- Back office: the categories page draws the tree; the create form has a "Department" select (top-level only); each row
  has a "Move to" select.

## D6. The seed

A category may carry `"parent": "<slug>"`. The checker requires that slug to be a category declared somewhere (any
vertical), to have no parent itself, and refuses a cycle. The seeder creates categories without a parent first, then
the rest with `parentCategoryId`, and for a category already there whose parent differs, calls the move endpoint -
idempotent, so a stack seeded by #360 is rearranged by the next run.
