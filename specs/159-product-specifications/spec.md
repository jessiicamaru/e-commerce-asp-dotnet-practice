# Feature Specification: Product specifications per category

**Feature Branch**: `feature/366-product-specifications`
**Created**: 2026-10-08
**Status**: Draft
**Issue**: #366
**Input**: the third piece the user chose after "sell anything, not only cameras" (#359): "thông số sản phẩm - mỗi danh mục
khai báo thuộc tính (thương hiệu, chất liệu...), seller điền, trang sản phẩm hiện bảng thông số, lọc theo thuộc tính".

## Why this exists

A product is a name, a description, a category and its variants' options. What a shopper compares - a camera's sensor,
a phone's screen, a shirt's material, a book's author - is buried in descriptions written differently by every seller,
so it cannot be laid out as a table, translated field by field, or filtered on. `docs/architecture/microservices-design.md`
lists "brands / dynamic product attributes" among what was never built.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper reads a product's specifications (Priority: P1)

The product page shows a "Specifications" table - Brand: Sony, Sensor: Full-frame, Resolution: 33 MP - in the reader's
language: a choice's option is translated, a text value (a model number, a measurement) is shown as written.

**Independent Test**: a product with specification values reads them back on its lookup, in `vi` and `en`, in the
order its category declares them.

**Acceptance Scenarios**:

1. **Given** a camera filed under "Máy ảnh không gương lật" (under "Máy ảnh"), **When** its page opens, **Then** it shows
   the department's specifications (Brand) and the category's own (Sensor, Resolution), department first.
2. **Given** an option with an English translation, **When** read in English, **Then** the English is shown; without
   one, the Vietnamese (per-field fallback, like every other text, specs/021).
3. **Given** a product with no values, **When** its page opens, **Then** there is no specifications table.

### User Story 2 - A shopper filters by a specification (Priority: P1)

Choosing a category in the storefront offers its choice specifications as filters - Brand: Apple, Material: Cotton - and
the listing shows only products with every option chosen.

**Independent Test**: filter by an option and get exactly the products whose value is that option; two options of
different specifications narrow to products with both.

**Acceptance Scenarios**:

1. **Given** Electronics with Brand: Apple / Samsung / ..., **When** a shopper picks Apple, **Then** the listing shows the
   Apple products under Electronics and nothing else.
2. **Given** a filter on a text specification, **When** offered, **Then** it is not: only choices are filterable.

### User Story 3 - Staff declare specifications and sellers fill them in (Priority: P1)

An administrator adds a specification to a category (name in both languages, text or choice, the choice's options in
both languages), renames, translates and removes one. A seller fills in their product's values; staff can for any
product.

**Acceptance Scenarios**:

1. **Given** a category, **When** an administrator adds "Thương hiệu" (choice: Apple, Samsung), **Then** every product
   under that category and, if it is a department, under its categories, can be given a brand.
2. **Given** a specification or option some product uses, **When** deleted, **Then** 409 saying how many products use it.
3. **Given** a seller's approved product, **When** the seller changes its specifications, **Then** it goes back to
   review and off the shelf (as with any change to what a shopper reads, specs/045).
4. **Given** a value for a specification that does not apply to the product's category, an option of another
   specification, an empty text or a text over 200 characters, **When** sent, **Then** 400 naming it.
5. **Given** another seller's product, **When** a seller sets its specifications, **Then** 404 (`SellerOwnership`).

### User Story 4 - The seed declares specifications (Priority: P2)

Every vertical declares its specifications and every seeded product fills them in, so the demo shows tables and filters.

### Edge Cases

- **A category moved to another department** (specs/158), or a product moved to another category: values for
  specifications that no longer apply are kept but not shown or offered; moving back shows them again.
- **A product deleted**: its values go with it (cascade).
- **A category deleted**: allowed only when empty (specs/024, 158); its specifications go with it.
- **The read cache** (specs/157): the new tables are catalogue tables; any write to them empties it.
- **Personal data** (specs/111): none of the new tables is about a person; declared so.

## Requirements *(mandatory)*

- **FR-001**: A category declares specifications: code (unique in the category, fixed), name with translations, kind
  `Text` or `Choice`, position; a choice has options: code (unique in the specification), value with translations.
- **FR-002**: A product's applicable specifications are its category's and its category's department's, department first.
- **FR-003**: `PUT /api/products/{id}/specifications` replaces the product's values; rules of Story 3 scenario 4; a
  seller's approved product goes back to review; audited.
- **FR-004**: The product lookup carries `specifications` (applicable, with values, in the reader's language).
- **FR-005**: `GET /api/products?optionIds=...` keeps products having every given option.
- **FR-006**: `GET /api/categories/{id}/specifications` (anyone) lists what applies to products in that category, with
  options, in the reader's language; administrators create, rename, translate and delete specifications and options
  under `/api/categories/{id}/specifications/...`; deleting what a product uses is 409.
- **FR-007**: Storefront: specifications table on the product page, choice filters for the chosen category. Seller's
  product page: fill them in. Back office: manage them per category.
- **FR-008**: The seed declares specifications per vertical and values per product; the checker validates them.

## Success Criteria *(mandatory)*

- **SC-001**: Server tests against PostgreSQL for every rule above, the review hook, the filter and the lookup in two
  languages; a mutation shown failing.
- **SC-002**: Client tests for the table, the filter, the seller's editor and the back office's management.
- **SC-003**: On the stack, every seeded product shows a specifications table; filtering Electronics by Brand: Apple
  lists the 4 Apple products; Bruno covers each endpoint and its refusals; `docs/reference` regenerated.

## Assumptions

- Text values are not translated: they are model numbers, measurements and names (research D2). Words go in a choice.
- Only choices are filterable; one option per specification at a time in the storefront's filter.
