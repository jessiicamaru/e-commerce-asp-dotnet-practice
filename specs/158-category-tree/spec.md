# Feature Specification: Categories in a tree - departments and their categories

**Feature Branch**: `feature/363-category-tree`
**Created**: 2026-10-08
**Status**: Draft
**Issue**: #363
**Input**: the second of the pieces the user chose after "sell anything, not only cameras" (#359): a category tree. The
schema has had `categories.ParentCategoryId` (a `RESTRICT` foreign key) since the start, and `POST /api/categories`
accepts it, but nothing reads it, nothing checks it, and no screen offers it.

## Why this exists

Since specs/156 the demo shop sells cameras, phones, laptops, clothing, shoes, kitchen goods, books and sports gear -
thirteen categories in one flat list in the filter, the hero and the seller's form. A shopper looking for a phone scans
past "Fiction" and "Outdoors"; filtering by "Electronics" is impossible because there is no such thing.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper browses by department (Priority: P1)

The storefront groups categories under departments (Điện tử, Thời trang, Nhà cửa, Sách, Thể thao, Máy ảnh). Choosing
a department shows everything in it; choosing a category under it narrows to that. A product page says where the
product sits.

**Why this priority**: it is what the tree is for, and what a visitor sees.

**Independent Test**: filter by a department and get the products of every category under it; filter by one of those
categories and get only its own.

**Acceptance Scenarios**:

1. **Given** "Điện tử" with "Điện thoại" and "Laptop" under it, **When** a shopper filters by "Điện tử", **Then** the
   phones and the laptops are listed (and anything filed directly under "Điện tử").
2. **Given** the filter, **When** it opens, **Then** each department is followed by its categories, indented, and a
   category with no department stands on its own.
3. **Given** the landing page, **When** it shows category chips, **Then** they are the departments.
4. **Given** a product in "Điện thoại", **When** its page opens, **Then** a breadcrumb reads "Điện tử › Điện thoại",
   each a link to the listing filtered by it, in the reader's language.

### User Story 2 - An administrator shapes the tree (Priority: P1)

In the back office an administrator creates a category under a department and moves an existing one into, out of or
between departments.

**Why this priority**: without it the tree can only come from the seed.

**Independent Test**: create a department and a category under it; move the category to another department, then to the
top; each move shows at once in the storefront.

**Acceptance Scenarios**:

1. **Given** a department, **When** a category is created with it as parent, **Then** it is listed under it.
2. **Given** a category, **When** it is moved (`PUT /api/categories/{id}/parent`), **Then** its parent changes, the move
   is audited (`CategoryMoved`), and its name, slug and translations do not.
3. **Given** a parent that does not exist, is the category itself, or is not a department (it has a parent of its own),
   **When** asked, **Then** 400 naming the reason. **Given** a category that has subcategories, **When** it is moved
   under another, **Then** 400 - it would make a third level.
4. **Given** a department with subcategories, **When** deleted, **Then** 409 saying how many - not the foreign key's 500.

### User Story 3 - The seed files each vertical under a department (Priority: P2)

The seed catalogue declares a department per vertical and files each category under it; the checker validates the
tree; seeding an existing stack moves categories already there into place.

**Independent Test**: `python seed/catalogue.py` passes; seeding the stack from #360 leaves 6 departments with the 13
categories under them; a second run changes nothing.

### Edge Cases

- **Categories from before**: every existing category has no parent and stays a top-level category - a department with
  no subcategories, which is exactly how it behaved. Nothing moves until somebody moves it.
- **A product filed under a department**: allowed; it is listed under the department, not under any subcategory.
- **Only two levels**: deliberate (research D1). A third level is refused rather than stored and half-shown.
- **The read cache** (specs/157): a move writes `categories`, so the cached listings and category list are emptied.

## Requirements *(mandatory)*

- **FR-001**: A category's parent, when it has one, exists, is not itself, and has no parent - checked on create and on
  move (400 otherwise); a category with subcategories cannot be given a parent.
- **FR-002**: `PUT /api/categories/{id}/parent` (Admin) with `{ "parentCategoryId": guid | null }` moves a category,
  audited as `CategoryMoved`; the existing `PUT /api/categories/{id}` is unchanged.
- **FR-003**: Deleting a category with subcategories is 409 naming the count.
- **FR-004**: `GET /api/products?categoryId=` includes products filed under the category's subcategories.
- **FR-005**: The storefront's filter lists departments with their categories under them; the hero's chips are the
  departments; the product page shows the breadcrumb.
- **FR-006**: The back office shows the tree, creates a category under a department and moves one.
- **FR-007**: The seed declares a department per vertical (`parent` on a category), the checker validates it, and the
  seeder creates parents first and moves categories already there.

## Success Criteria *(mandatory)*

- **SC-001**: Server tests: each rule of FR-001 to FR-004 against PostgreSQL, the move audited, the listing including
  subcategories.
- **SC-002**: Client tests: the tree built from the flat list, the filter's grouping, the hero's departments, the
  breadcrumb, the back office's create and move requests.
- **SC-003**: On the stack, the seed leaves 6 departments with 13 categories under them and filtering by "Điện tử"
  lists 7 products.
- **SC-004**: Bruno covers the new endpoint and its 400/403/404; `docs/reference` regenerated.

## Assumptions

- Two levels are enough for this shop (research D1).
- Category order is by name, as it is today.
