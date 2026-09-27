# Feature Specification: A shop has a page

**Feature Branch**: `099-shop-page` | **Created**: 2026-09-27 | **Issue**: #197

**Status**: Draft

**Input**: Issue #197 - "a shopper cannot see a seller's shop".

## Why

A product page says which shop sells it (specs/036), but the name leads nowhere. There is no page for a shop, no list of
what one seller sells, and a seller has nothing to say about their shop beyond its name. The public listing cannot be
asked for one seller's products (`GetProductsQuery` takes a category, a search and a sort - no seller), although the
repository already filters by seller for the seller's own list.

## User Scenarios & Testing *(mandatory)*

### US1 - A shopper opens a shop from a product (Priority: P1)

The shop's name on a product page is a link to `/shops/{sellerId}`: the shop's name and description, and its products on
the shelf, paged like the catalogue.

**Why this priority**: The issue.

**Independent Test**: From a seller's product, open its shop: only that seller's products on the shelf are listed.

**Acceptance Scenarios**:

1. **Given** a seller with products on sale and one waiting for review, **When** a shopper opens the shop, **Then** only
   the ones on sale are listed, with the shop's name and description.
2. **Given** the shop's own products (no seller), **Then** the product page says "the shop" without a link - there is no
   shop page for the shop itself (Decision).
3. **Given** an unknown seller id, a seller Catalog has not heard of yet, or a suspended seller (specs/095), **Then** the
   shop page is 404.

---

### US2 - A seller describes their shop (Priority: P1)

A seller writes a short description of their shop (at most 500 characters) where they rename it; the shop page shows it.
Clearing it removes it.

**Why this priority**: Without it a shop page is a name and a grid.

**Independent Test**: Describe the shop; its page shows the words; clear them; the page shows none.

**Acceptance Scenarios**:

1. **Given** a seller, **When** they save a description, **Then** Identity stores it and announces it, and the shop page
   shows it once Catalog has recorded it.
2. **Given** a description longer than 500 characters, **Then** 400.
3. **Given** a customer who is not a seller, **Then** 404 (as for renaming, specs/027).

### Edge Cases

- **Out-of-order announcements.** A description is recorded only if newer than the one stored (the read model's timestamp
  guard, separate from the name's).
- **A description announced before the registration arrives.** The sellers row is created with the description and an
  empty name the registration fills in (the specs/095 pattern); the shop page is 404 until a name exists.
- **Moderation.** None - like the shop name, the description is the seller's to change (Decision).
- **Words that are HTML.** Shown as text, never as markup.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `GET /api/products?sellerId=` lists one seller's products on the shelf, with every other filter.
- **FR-002**: `GET /api/shops/{sellerId}` (anyone) returns the shop's id, name, description and how many products it has on
  the shelf; 404 when the seller is unknown, unnamed or suspended. The gateway routes `/api/shops`.
- **FR-003**: `PUT /api/sellers/me/description` (Seller) sets or clears the caller's description (≤ 500), audited, and
  publishes `SellerDescribedEvent(SellerId, Description, DescribedAt)` in the same save.
- **FR-004**: Catalog records the description on its `sellers` read model, guarded by `DescriptionObservedAt`.
- **FR-005**: `GET /api/sellers/me` includes the description.
- **FR-006**: The storefront has `/shops/:sellerId`, a link from the product page's shop name, and a description field
  beside the seller's rename.
- **FR-007**: Migrations add `seller_profiles.Description` (Identity) and `sellers.Description`, `DescriptionObservedAt`
  (Catalog) - expand only.

### Key Entities

- **Seller profile** (Identity) - shop name, now a description.
- **Seller** read model (Catalog) - name, description, suspension.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Catalog tests: the seller filter lists only that seller's products on the shelf; the shop read (name,
  description, count; 404 unknown, unnamed, suspended); the description recorded newest-wins.
- **SC-002**: Identity tests: a seller describes their shop and the announcement carries it; too long is 400; a
  non-seller 404.
- **SC-003**: Storefront tests: the shop page lists the seller's products with the header; the product page links the
  shop; the seller saves a description.
- **SC-004**: Bruno: the seller describes their shop; the shop page and its products are public.
- **SC-005**: Mutations - the seller filter ignored, a suspended shop served, the description guard removed - each red.

## Decision

1. **No page for the shop itself.** Its own products are a curation, not a seller; the catalogue is its page. Recorded as
   decided on the user's behalf ([research.md](research.md) D2).
2. **The description is not moderated**, like the name (specs/027): a seller's words about their own shop, changed by
   them, shown as text. A rejected product keeps moderation where selling is at stake (D3).
3. **The shop read lives in Catalog**, from its read model - an anonymous page costs no call to Identity (specs/027's reason
   for the name) (D1).

## Assumptions

- Sellers are identified by their Identity user id everywhere (specs/027).

## Out of scope

- A shop logo or banner; shop ratings; following a shop.
