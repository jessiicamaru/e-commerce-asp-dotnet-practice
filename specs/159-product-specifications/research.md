# Research: Product specifications per category

## D1. Declared per category, inherited from the department

**Decision**: a category declares specifications; a product has those of its category and of its category's department
(specs/158 makes that one parent at most).

**Rationale**: "Brand" is true of everything in Electronics; "Sensor" only of cameras. Declaring Brand once on the
department and Sensor on the camera categories is how a person would write it, and two levels means "inherited" is one
lookup, not a walk.

**Alternatives rejected**: free key-value pairs per product (every seller spells "Thương hiệu" differently, nothing is
filterable); a global attribute list (a book's "Author" offered for a shirt); inheritance from a deeper tree (there is
none).

## D2. Two kinds: text and choice; only choices translated and filterable

**Decision**: `Text` holds a value as written (`6,1 inch`, `ILCE-7M4`, `Paulo Coelho`), not translated. `Choice` picks an
option the category lists, each option translated (`Bông` / `Cotton`).

**Rationale**: what needs translating is words, and words that repeat across products are exactly what a choice is.
A free text translated per product would be the description again. Filtering needs equal values to be equal; options
are, typed text is not ("Apple", "apple", "APPLE").

**Alternatives rejected**: translated text values (a fourth table and an editor per language for numbers); numeric
specifications with ranges (no screen asks for it yet).

## D3. Five tables, expand-only

`category_specifications`, `category_specification_translations`, `specification_options`,
`specification_option_translations`, `product_specifications` - one migration, no existing column touched, so an older
image runs against it (CLAUDE.md, the schema rule). Foreign keys:
- category → specifications: cascade (a category can only be deleted empty, specs/024/158);
- specification/option → product values: restrict - deleting what a product uses is a worded 409, checked first;
- product → its values: cascade (deleting a product takes them, as it takes its variants).

## D4. Writing a product's values

One `PUT /api/products/{id}/specifications` with the whole set, replacing it - the seller's page edits them as one form,
and a replace is idempotent (the seeder reruns it). Ownership is `SellerOwnership`, like every product write (404 for
another seller's). A seller's change to an approved product calls `ProductReview.AfterSellerEditAsync`: specifications
are what a shopper reads (CLAUDE.md: "a tenth edit of what a shopper reads must call it too"). Unchanged values do not.

## D5. The filter

`optionIds` on the listing: for each id, `EXISTS (product_specifications WHERE ProductId = p.Id AND OptionId = id)`.
An index on `product_specifications(OptionId)` serves it. The storefront offers one option per specification at a time,
so AND across ids is the whole semantics.

## D6. Where it shows

- Lookup (`GET /api/products/{id}`): `specifications` - applicable ones that have a value, in declared order.
- `GET /api/categories/{id}/specifications`: for the filter and the seller's form; cached like the other public reads
  (specs/157), the new tables added to `CatalogueWrites`.
- The listing does not carry them (a page of 12 products × their specifications for a card that does not show them).

## D7. Seed

Categories carry `specifications` (code, vi, en, kind, options with code/vi/en); products carry `specifications` as
`{code: optionCode | text}`. The checker requires each product's codes to be specifications of its category or
department and each option code to exist; the seeder creates by code (idempotent) and PUTs each product's set.
