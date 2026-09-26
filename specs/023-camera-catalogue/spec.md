# Feature Specification: A Catalogue of Real Cameras

> Written on 2026-09-27, after the feature merged (#60), from the code at that merge, the pull request
> and docs/features/catalog.md.

**Feature Branch**: `023-camera-catalogue` · **Created**: 2026-09-22 (the pull request; this record was
written afterwards) · **Status**: Implemented

**Merged**: [#60](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/60), 2026-09-22 (23:29, UTC+7), as `06ad8e3`

**Input**: The pull request "feat(catalog): a catalogue of real cameras, seeded through the API". No
issue is linked to it; who asked for it and when is not recorded.

> `docs/project/timeline.md` lists 023 among the "smaller changes made without a separate design
> record". This directory is that record, reconstructed on 2026-09-27.

## Why this exists

"The catalogue held 94 products and every one of them was debris: `iPhone 16 Pro Max 1790085352812`,
`E2E Widget …`, `CI Widget …`. Nothing anybody would click." The storefront had become able to show
variants (specs/020), two languages (specs/021) and two price lists (specs/022), and there was nothing
in the shop that exercised any of it.

## User Scenarios & Testing

### User Story 1 - A shopper sees cameras they recognise (Priority: P1)

Fourteen real cameras - Sony A7 IV, Canon EOS R6 Mark II, Nikon Z6III, Fujifilm X-T5, X100VI, Ricoh GR
IIIx and the rest - in the shapes they are sold in (body only, a kit with a lens, black or silver), in
Vietnamese and English, priced in dong and in dollars, with stock.

**Why this priority**: it is the feature: a catalogue somebody would click through.

**Independent Test**: seed a started stack, then read the catalogue in `vi`/`VND` and in `en`/`USD`.

**Acceptance Scenarios**:

1. **Given** a seeded stack, **When** the catalogue is read, **Then** the 14 cameras are there in two
   categories (mirrorless and compact), with 23 variants between them.
2. **Given** a variant, **When** it is read in Vietnamese and dong, **Then** its options and price read
   `Bộ: Chỉ thân máy · Màu: Đen  42.000.000 ₫`; in English and dollars `Kit: Body only · Colour:
   Black  $1,699.00`.
3. **Given** a seeded variant, **When** a shopper tries to buy it, **Then** it has stock to sell.

### User Story 2 - An operator seeds a stack with one command, as often as they like (Priority: P1)

**Why this priority**: equal first; a seeder that can be run only once, or that half-fails and cannot
be finished, is a liability.

**Independent Test**: run the seeder twice.

**Acceptance Scenarios**:

1. **Given** a started stack and the administrator's credentials, **When** the seeder runs, **Then**
   every row goes through the gateway as an administrator would send it.
2. **Given** a stack already seeded, **When** the seeder runs again, **Then** it adds nothing and says
   `0 product(s) added, 14 already there`.
3. **Given** a run that stopped part way, **When** it runs again, **Then** it finishes the job:
   translations, dollar prices and stock are rewritten, missing rows are added.
4. **Given** anything already in the catalogue, **When** the seeder runs, **Then** it deletes nothing.
5. **Given** a variant just created, **When** Inventory has not heard of it yet, **Then** the seeder
   waits for it rather than failing.

### User Story 3 - A variant reads the same way everywhere (Priority: P2)

Found by seeding: two shapes of one camera listed their options in opposite orders.

**Why this priority**: a correctness defect in shipped code, but one only a catalogue with two-option
variants could show.

**Independent Test**: add a variant whose options are entered in the opposite order to its sibling's;
both summaries list them in the same order.

**Acceptance Scenarios**:

1. **Given** two variants whose options were entered in opposite orders, **When** either is read,
   **Then** both summaries list the options in the same order.
2. **Given** a variant, **When** it is read naming a language and naming none, **Then** the summaries
   list the options in the same order (the translated one in the other language's words).
3. **Given** summaries stored before the fix, **When** the migration runs, **Then** they are rewritten
   in that order.

### User Story 4 - An option's translation can be set through the API (Priority: P2)

Found by seeding: the endpoint that translates a variant option (specs/021) could not be called.

**Why this priority**: an endpoint nothing can reach is a defect; the seeder is its first client.

**Independent Test**: read a product; every variant option carries an id; send that id to the
translation endpoint.

**Acceptance Scenarios**:

1. **Given** a product read over HTTP, **When** its variants' options are listed, **Then** each carries
   its `id`.
2. **Given** that id, **When** `PUT /api/products/{id}/options/{optionId}/translations/en` is sent,
   **Then** the option reads in English.

### Edge Cases

- **Options come back in no particular order.** Pairing each option with its translation by position
  gave `Bộ: Chỉ thân máy` the English `Colour: Black`; the seeder matches by name.
- **The first variant reuses the product's id and carries the product's SKU** (specs/020), so the
  first variant's own SKU in `cameras.json` (e.g. `SONY-A7M4-BODY`) is never stored; the seeder handles
  it separately and never adds a duplicate shape.
- **Inventory learns of a variant through the broker**, a moment after Catalog commits; a stock write
  in between is a correct 404 (seen on the second run, not the third).
- **The 94 junk products stay**: there was no delete endpoint, and removing them by SQL is destructive.
  A dollar listing sorted cheapest first is therefore "a wall of `$0.01` widgets".
- **Category names are Vietnamese only** - translating them was still out of scope (specs/021), so an
  English reader sees "Máy ảnh không gương lật".

## Requirements

- **FR-001**: The seed data MUST name real camera models, in the shapes they are sold in, with text in
  Vietnamese and English, a price in dong and a price in dollars per variant, and stock.
- **FR-002**: The seeder MUST write only through the gateway's public API, signed in as an
  administrator - never to a database.
- **FR-003**: The seeder MUST be idempotent by SKU and MUST NOT delete anything.
- **FR-004**: The two price lists MUST NOT be conversions of each other (specs/022).
- **FR-005**: The data MUST say, where a reader will see it, that the prices are approximate and were
  not checked against any shop.
- **FR-006**: Every variant option in an HTTP response MUST carry its id.
- **FR-007**: A variant's options MUST be summarised in one fixed order, whether the summary is stored
  or rebuilt in a language, and whether the options were entered in one order or another.
- **FR-008**: Summaries stored before FR-007 MUST be rewritten to that order, without a schema change.
- **FR-009**: The seeder MUST match an option to its translation by name, never by position.
- **FR-010**: No image may be invented: products are seeded with none.

### Key Entities

- **Seed catalogue** (`server/seed/cameras.json`) - categories, and products each with Vietnamese and
  English text, a category, and variants each with a SKU, options in both languages, a dong price, a
  dollar price and a stock count.
- **Variant option** - a name and value on a variant, now addressable by its id.
- **Option summary** - the one-line description of a variant's options, stored on the variant and
  frozen onto order lines.

## Success Criteria

- **SC-001**: After one run: 14 products, 23 variants, 2 categories, every variant priced in VND and
  USD and stocked.
- **SC-002**: A second run adds 0 products.
- **SC-003**: The ordering test fails when the fix is removed, and passes with it.
- **SC-004**: The whole suite, Bruno, `verify-auth.sh` and `verify-saga.sh` pass with the change (245
  tests; Bruno 81/81 requests, 122 tests).

## Assumptions

- The stack is started (all services, the broker) and `ADMIN_EMAIL` / `ADMIN_PASSWORD` are the
  seeded administrator's.
- The prices come "from a model's knowledge up to May 2026, not from any shop": right order of magnitude
  and right relative order only.
- Python 3 with its standard library only.

## Out of Scope

- **Images** - "no honest way to obtain product photographs here".
- **Translating category names** (specs/026).
- **Removing the 94 junk products** (a delete endpoint came with specs/024; a cleaner with specs/073).
- **Clicking through the storefront in a browser** - still not done at this merge.
