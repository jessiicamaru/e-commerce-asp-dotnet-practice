# Feature Specification: A Storefront That Looks Like a Shop

> Written on 2026-09-27, after the feature merged (#62), from the code at that merge, the pull request and docs/architecture/storefront.md.

**Feature Branch**: `025-storefront-redesign`

**Created**: 2026-09-22

**Status**: Merged as [#62](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/62) on 2026-09-22

**Input**: The owner asked for a redesign and sent a reference design. The storefront was a functional
shadcn page: the default neutral theme, small cards, and a grey tile with a letter in it where a
photograph should be.

## Context

The storefront exists to exercise the API as a person would (CLAUDE.md), and until this feature
nobody had looked at it as a person would: seven features had shipped with "nobody has clicked through
the storefront" as a standing caveat. The redesign takes the reference's **design language** - light,
bento, round, airy, one accent - and none of its branding, copy or invented statistics.

Looking at the page is also what found a backend gap: 95 categories, 93 of them test debris, all
offered in the filter a shopper uses, and no way to remove one through the API. So this feature carries
one server endpoint, `DELETE /api/categories/{id}`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper lands on something that reads as a shop (Priority: P1)

A visitor opens the catalogue and sees a warm, light page with one accent colour, a hero that says
what the shop sells, a handful of categories to steer by, one real featured camera at its real price,
and a grid of large cards.

**Why this priority**: It is what the owner asked for. Every other story is a part of it.

**Independent Test**: Open `/` with no search or filter and look at it at 1440px and at 500px.

**Acceptance Scenarios**:

1. **Given** the unfiltered first page, **When** it loads, **Then** a hero shows the product count, up
   to six category chips and one featured product - the dearest on the page, not the first tile.
2. **Given** a search term, a category or a page other than the first, **When** the catalogue loads,
   **Then** no hero is shown and the results come first.
3. **Given** the hero, **When** it is read, **Then** every statement in it is true of this shop: no
   review counts, no "new" badges, nothing the shop cannot know.

---

### User Story 2 - A shopper can search from any page (Priority: P2)

The search box lives in the floating top bar, so a shopper looking at one camera can look for another
without going back first.

**Why this priority**: A navigation improvement the layout made possible; the catalogue page already
had search.

**Independent Test**: On a product page, type a term in the bar and submit.

**Acceptance Scenarios**:

1. **Given** any page, **When** a shopper submits a term, **Then** they land on `/?q=<term>`, the same
   address the catalogue's own filter writes, so a search stays shareable.
2. **Given** an empty term, **When** it is submitted, **Then** they land on `/`.
3. **Given** a signed-in shopper with lines in their cart, **When** any page loads, **Then** the bar shows
   how many lines; a signed-out visitor's page does not ask for a cart at all.

---

### User Story 3 - A product without a photograph does not look broken (Priority: P3)

The catalogue has no photographs yet. A product without one shows a deliberate tile - an aperture on a
pale tint derived from the product's id - rather than a grey box with a letter.

**Why this priority**: It is the most visible thing on a grid with no photographs, but it is a fallback.

**Independent Test**: Load the grid with no images and see varied, stable tiles; break an image URL and
see the same tile.

**Acceptance Scenarios**:

1. **Given** a product with no image, **When** it is shown anywhere, **Then** it gets the same tint
   every time, because the hue comes from its id.
2. **Given** an image that fails to load, **When** the error fires, **Then** the tile replaces it.

---

### User Story 4 - A product page presents the object and its choices (Priority: P3)

The product page puts the picture on its own panel, shows "Sold by The shop" under the name, and offers
variants as whole clickable cards rather than radio rows.

**Why this priority**: Part of the same redesign; the product page already worked.

**Independent Test**: Open a product with two variants and choose each by clicking anywhere on its card.

**Acceptance Scenarios**:

1. **Given** a product with variants, **When** a shopper clicks anywhere on a variant's card, **Then** it
   is selected; the radio inside is still present and labelled for keyboards and screen readers.
2. **Given** any product, **When** its page or card is shown, **Then** it reads "Sold by The shop" - laid
   out now so that sellers (specs/027) change a word, not the layout.

---

### User Story 5 - An administrator removes a category nobody uses (Priority: P2)

An administrator deletes a category that has no products filed under it, so debris stops appearing in
the shopper's filter. A category that still has products is refused with a sentence saying how many.

**Why this priority**: Found by looking at the page; the filter a shopper uses was mostly junk.

**Independent Test**: Create a category, delete it (204), and see it gone from `GET /api/categories`;
file a product under another and see its deletion refused with 409.

**Acceptance Scenarios**:

1. **Given** an empty category, **When** an administrator deletes it, **Then** it is gone.
2. **Given** a category with one product, **When** an administrator deletes it, **Then** it is refused with
   409 naming the category and "1 product(s)".
3. **Given** that product is deleted, **When** the category is deleted again, **Then** it succeeds.
4. **Given** no category has the id, **When** it is deleted, **Then** the answer is 404.

---

### Edge Cases

- **A narrow screen.** At 500px the bar wraps onto several rows; `rounded-full` on a tall box is a
  stadium, so the bar is a pill only while it is one row (`rounded-3xl`, `sm:rounded-full`).
- **A reported 430px overflow that did not exist.** Headless Chrome lays out at a 500px minimum and had
  cropped the screenshot; see [research.md D6](./research.md).
- **The translated "Back to the shop" string already had an arrow**, so the page rendered `← ← Back`.
- **The featured product repeating itself** as the first grid tile immediately below it.
- **Signed out.** The bar does not request a cart, which would be a guaranteed 401.
- **Category names in English.** Still untranslated at this merge - `Máy ảnh không gương lật` in an
  English page. Closed by specs/026.
- **Dark mode.** Tokens exist; nothing toggles them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The storefront MUST present one warm light theme with a single saturated accent that
  carries dark text at an accessible contrast.
- **FR-002**: The unfiltered first page of the catalogue MUST show a hero containing only facts: the
  product count, categories, and one real product at its real price.
- **FR-003**: The hero MUST NOT be shown once a shopper has searched, filtered or paged.
- **FR-004**: Search MUST be available from the top bar on every page and MUST write the same address the
  catalogue reads.
- **FR-005**: A product without a usable image MUST show a stable, deliberate placeholder.
- **FR-006**: A whole product card MUST be one link, and a whole variant card MUST be one choice, with the
  accessible control kept.
- **FR-007**: Product cards and the product page MUST name who sells the product.
- **FR-008**: An administrator MUST be able to delete a category with no products filed under it; a
  category with products MUST be refused with the count; a missing one MUST be 404.
- **FR-009**: The debris cleaner MUST remove categories that `cameras.json` does not name, relying on the
  API's refusal to keep any that still hold products.

### Key Entities

- **Theme tokens**: the colour, radius and surface values in `client/src/index.css` every component
  reads.
- **Category**: a Catalog row products are filed under; deletable only when empty.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The catalogue, the product page and the top bar render without horizontal scrolling at
  1440px and at 500px. The PR verified 500px only; narrower than that is **not verified**.
- **SC-002**: Nothing in the hero is untrue of the shop at the moment it is shown.
- **SC-003**: After the cleaner runs, the category filter offers only real categories. The PR records 93
  junk categories removed.
- **SC-004**: Every test project passes with category deletion covered, including the refusal (255 at the
  merge, 4 new).
- **SC-005**: Type-check, lint and build are clean.

## Assumptions

- The reference design is read for its language only; its branding, copy and statistics are not the
  shop's to use.
- There are no product photographs yet and no honest way to obtain them in this feature (they arrived
  with specs/030).
- The storefront's purpose is exercising the API; the redesign should not add behaviour the API does not
  support.
- Sellers are the next feature, so "Sold by" is laid out with the shop's own name.

## Out of Scope

- Translating category names (specs/026).
- Product photographs (specs/030).
- A dark-mode toggle.
- Client unit tests: the client had none at this merge; they arrived with specs/028.
- Verification narrower than 500px.
