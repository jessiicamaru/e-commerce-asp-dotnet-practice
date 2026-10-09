# Feature Specification: A gallery of photographs per product

**Feature Branch**: `feature/368-product-gallery`
**Created**: 2026-10-09
**Status**: Draft
**Issue**: #368
**Input**: "A product has an ordered gallery of photographs; the first is its cover. The seller adds, removes and
reorders them; the product page shows a gallery. Every rule the single photograph has keeps holding."

## Context

A product has had exactly one photograph since specs/019 (`products.ImageContentType`, `ImageUpdatedAt`,
`ImageAccessKey`, the key derived from them), and each variant one of its own since specs/032. Since the shop sells
anything (specs/156, #359), one angle is not enough to buy a shirt, a pan or a backpack: the back, the label, the inside
and the size are unseen. Found in the review of 2026-10-09.

## User Scenarios & Testing

### User Story 1 - A shopper looks at every photograph (Priority: P1)

A shopper opens a product with several photographs and sees the cover large with the others as thumbnails beneath;
choosing a thumbnail shows that photograph large; on a phone the thumbnails scroll sideways.

**Why this priority**: it is what the feature is for, and the only part a shopper sees.

**Independent Test**: give a product a cover and two more photographs through the API; open its page; three thumbnails,
the cover shown; choose the third, it is shown.

**Acceptance Scenarios**:

1. **Given** a product with a cover and two photographs, **When** a shopper opens it, **Then** the cover is shown large
   and three thumbnails follow, in the gallery's order.
2. **Given** that page, **When** the shopper chooses the third thumbnail, **Then** that photograph is shown large.
3. **Given** a variant with its own photograph (specs/032), **When** it is chosen, **Then** its photograph is shown
   large, as today.
4. **Given** a product with only a cover, **Then** no thumbnail strip is drawn - the page looks as it does today.
5. **Given** the listing, **Then** each card still shows the cover only.

---

### User Story 2 - A seller builds the gallery (Priority: P1)

On the seller's product page the photographs are shown in order with the cover marked; the seller adds several at once,
removes one, moves one earlier or later, and makes another the cover.

**Why this priority**: without it nothing fills a gallery.

**Independent Test**: as the seller, add three photographs to a product with no cover, move the last first, make the
second the cover, remove one; read the product back after each step.

**Acceptance Scenarios**:

1. **Given** a product with no photograph, **When** the seller adds one, **Then** it becomes the cover (what the
   listing shows).
2. **Given** a product with a cover, **When** the seller adds photographs, **Then** they follow it in the order added,
   up to 10 photographs in all; the eleventh is refused with a message naming the limit.
3. **Given** a gallery, **When** the seller moves a photograph, **Then** the new order is kept.
4. **Given** a gallery, **When** the seller makes a photograph the cover, **Then** it is the cover everywhere (listing,
   cart, order pictures) and the previous cover takes its place in the gallery.
5. **Given** a gallery with more than the cover, **When** the seller removes the cover, **Then** the first photograph
   becomes the cover; removing any other photograph removes just it.
6. **Given** an approved product, **When** its seller adds, removes, reorders or changes the cover, **Then** it goes back
   to review and off the shelf, as any photograph change does (specs/045). Staff changes do not.
7. **Given** another seller's product, **When** a seller tries any of these, **Then** it is 404 (specs/027).

---

### User Story 3 - A moderator reviews every photograph (Priority: P2)

A product waiting for review shows the moderator its whole gallery, not just the cover, since any of them may be what
needs a decision.

**Why this priority**: a gallery change sends a product to review; reviewing only the cover would let the rest through.

**Independent Test**: as staff, read the review queue for a product with three photographs: all three are named.

**Acceptance Scenarios**:

1. **Given** a product waiting with a cover and two photographs, **When** staff open the review queue, **Then** the row
   shows all three.

### Edge Cases

- **Off the shelf**: a photograph of a product not on sale is served only to an address with its own key (specs/081),
  like the cover; only responses their reader may see carry those addresses.
- **Concurrent cover changes**: one wins, the other is told to try again (409), and no row names a missing file.
- **A reorder naming the wrong photographs** (one missing, a stranger's id, a duplicate) is refused and changes nothing.
- **Deleting the product** deletes every photograph's bytes after the rows, like the cover's (specs/029).
- **The orphan report** knows every photograph's key; a file no row names is still an orphan (specs/033).
- **An older image** (rollback) ignores the new table: the cover keeps working; the other photographs are not shown.
- **The read cache**: a gallery change evicts the anonymous product read (specs/157).

## Requirements

### Functional Requirements

- **FR-001**: A product MUST hold up to **10 photographs** in a kept order: the **cover** and up to 9 more.
- **FR-002**: The cover MUST remain what `imageUrl` names on every product response and what the listing, cart and
  orders show; nothing that reads it today changes.
- **FR-003**: The product lookup MUST carry the other photographs in order, each with an id and an address; the listing
  MUST NOT (it shows the cover only).
- **FR-004**: The seller, or staff, MUST be able to **add** a photograph (it becomes the cover when there is none),
  **remove** one (removing the cover promotes the first of the rest), **reorder** the photographs after the cover, and
  **make** any photograph the cover (the previous cover takes its place).
- **FR-005**: Every one of those changes, by a seller on an approved product, MUST send it back to review (specs/045).
- **FR-006**: Each photograph MUST follow the rules of the cover: type from the bytes (JPEG, PNG, WebP; SVG refused),
  at most 2 MB, stored only when new, served for good only to a product on sale and otherwise only with its own access
  key, audited, and its bytes deleted only after no row names them.
- **FR-007**: A reorder MUST name exactly the product's current photographs after the cover, each once; anything else is
  refused and changes nothing.
- **FR-008**: The review queue MUST show staff every photograph of a product waiting.
- **FR-009**: The storefront MUST show a gallery on the product page and the seller's page MUST manage it (add several,
  remove, move, make cover); the back office review row MUST show the photographs.
- **FR-010**: The schema change MUST be expand-only: the cover's columns stay where they are.

### Key Entities

- **Product photograph**: one of a product's photographs after the cover - its position, its stored type, its key in the
  image store, when it was added, and its access key.
- **Cover**: the product's existing photograph (specs/019), unchanged.

## Success Criteria

- **SC-001**: A product page with a cover and 2 photographs shows 3 thumbnails in the gallery's order, in a browser.
- **SC-002**: Every operation of FR-004 reads back correctly on PostgreSQL, and a seller's change to an approved product
  leaves it Pending; dropping the review call fails a test.
- **SC-003**: After making a photograph the cover, the promoted photograph's old address is not served, the listing
  shows the new cover, and the orphan report finds no orphan.
- **SC-004**: The 11th photograph is refused; a reorder with a missing or foreign id is refused and changes nothing.
- **SC-005**: Bruno covers every new endpoint and its refusals; `docs/reference` regenerated.

## Assumptions

- **10** in all is enough for a marketplace listing and keeps the page and the review bounded.
- The seed adds no photographs: there is no honest source for them (`seed-catalogue.py` says so); the cameras'
  credited photographs stay uploaded by hand.
- Captions and per-photograph alt text are out of scope; the product's name is the alt text, as today.
- Video is out of scope.
