# Feature Specification: A shop for anything, not only cameras

**Feature Branch**: `feature/359-generic-catalogue`
**Created**: 2026-10-08
**Status**: Draft
**Issue**: #359
**Input**: "I don't want to sell only cameras any more - help me sell other kinds of things, anything; cameras were only
an example at the start." Decided with the user: of four possible pieces (wording and seed, a category tree, product
specifications, caching), this feature is the first only.

## Why this exists

The model already sells anything: a variant's options are free text ("a camera has kits, a shirt has sizes, a phone has
storage", `ProductVariant`), and categories are rows an administrator creates. What still says "camera shop" is
everything around it:

- the search box ("Search cameras…" / "Tìm máy ảnh…"), the landing hero ("Cameras - Take the picture you are looking
  at."), the open-shop page ("Sell your own cameras and lenses here") and the add-variant hint ("Another kit…");
- the tile drawn for a product with no photograph - a camera aperture, so a shirt without a photograph shows a lens;
- the seed: `seed-catalogue.py` reads one file, `cameras.json`, and `clean-test-debris.py` keeps only what it names, so
  the only demo catalogue there is is fourteen cameras.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The shop's own words fit whatever it sells (Priority: P1)

A shopper lands on the storefront and nothing in the interface assumes what is for sale; a person opening a shop is
not told they are selling cameras.

**Why this priority**: it is what a visitor sees first, and it is wrong for every product that is not a camera.

**Independent Test**: read the storefront in both languages: the search box, the hero, the open-shop page and the
add-variant form name no kind of goods. A product without a photograph shows a neutral tile.

**Acceptance Scenarios**:

1. **Given** the landing page in English or Vietnamese, **When** it is read, **Then** the hero and the search box say
   nothing about cameras, and stay true (no invented claims - the hero's own rule).
2. **Given** a product with no photograph, **When** its card is drawn, **Then** the tile shows a neutral glyph, tinted
   per product as before.
3. **Given** a future edit of the locale files, **When** it reintroduces camera wording into the shop's own sentences,
   **Then** a client test fails.

### User Story 2 - A demo catalogue across several kinds of goods (Priority: P1)

Whoever runs the stack seeds a catalogue a person would click through in a general marketplace: cameras still, and
phones and laptops, clothing and shoes, home and kitchen goods, books and sports goods - each in the shapes it is sold
in (sizes, colours, storage, cover), in Vietnamese and English, priced in dong and dollars, with stock.

**Why this priority**: the catalogue is what the demo, the screenshots and the thesis show; a camera-only catalogue
contradicts "sell anything".

**Independent Test**: `python seed/catalogue.py` validates every file; `seed-catalogue.py` against a started stack seeds
them all, a second run adds nothing; the storefront filters by each new category.

**Acceptance Scenarios**:

1. **Given** a started stack, **When** the seeder runs, **Then** every vertical's categories and products are created
   through the API, idempotent by SKU, with English text, dollar prices, option translations and stock.
2. **Given** the seeded stack, **When** the cleaner runs, **Then** it keeps every product and category named in any
   vertical's file and proposes nothing seeded for deletion.
3. **Given** a vertical file with a duplicate SKU, a missing English text, an unknown category, a dong price with a
   fraction, or variants of one product naming different options, **When** the checker runs (and CI runs it), **Then**
   it fails naming the file and the problem.

### Edge Cases

- **A stack seeded from `cameras.json` before this**: the cameras keep their SKUs and slugs, so the seeder skips them
  and adds the rest.
- **One vertical only**: `seed-catalogue.py cameras` seeds just that file (and the cleaner still keeps all of them).
- **Photographs**: only the cameras have credited photographs (`IMAGE-CREDITS.md`). The new products have none and show
  the neutral tile; a photograph is a licensing decision for whoever runs it, as before.
- **Prices**: approximate, from a model's knowledge, like the cameras' - each file says so.

## Requirements *(mandatory)*

- **FR-001**: The storefront's search placeholder, hero, open-shop subtitle and add-variant hint name no kind of goods,
  in both languages.
- **FR-002**: The no-photograph tile is a neutral glyph, not a camera part.
- **FR-003**: A client test fails when the shop's own sentences in `common`, `catalog` or `seller` mention cameras or
  lenses.
- **FR-004**: The seed catalogue is one JSON file per vertical in `server/seed/catalogue/`, in the existing format;
  cameras are one of them, unchanged.
- **FR-005**: The seeder and the cleaner read the same files through one loader, which validates them; the loader runs
  on its own as a checker, and CI runs it.
- **FR-006**: SKUs and category slugs are unique across all files.

## Success Criteria *(mandatory)*

- **SC-001**: At least five verticals besides cameras, each with at least one category and four products, most of
  them with two or more variants.
- **SC-002**: The seeder seeds every file against the compose stack with no error; a second run adds nothing; the
  cleaner then proposes no deletion.
- **SC-003**: The checker rejects each defect listed in Story 2, scenario 3 (shown by planting one of each).
- **SC-004**: Client tests, lint and build pass; the wording test fails with the old search placeholder restored.

## Assumptions

- Categories stay flat: the tree is a separate feature the user may ask for.
- No product attributes/specifications: also separate. What a product is lives in its description and its options.
- Real brand and model names are used, as the cameras did; descriptions are written here, not copied.
