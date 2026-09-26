# Feature Specification: Browse, Search and Open a Product

> Completed on 2026-09-27, after the feature merged (#46), from the code at that merge, the pull
> request and docs/features/catalog.md and docs/architecture/storefront.md.

**Feature Branch**: `016-storefront-catalog` · **Created**: 2026-09-22 · **Status**: Implemented

**Merged**: [#46](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/46), 2026-09-22 (02:54, UTC+7)

**Input**: Issue [#36](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/36), part of [#23](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/23)

## Why this exists

The storefront could sign people in but could not show them anything to buy. Browsing needs no
account, so every call here is anonymous.

## User Scenarios & Testing

### User Story 1 - A visitor browses the shop (Priority: P1)

**Why this priority**: the first thing anybody does in a shop; every later page (cart, checkout) starts
from a product somebody found here.

**Independent Test**: open `/` signed out; products appear, 12 to a page, with a pager.

**Acceptance Scenarios**:

1. **Given** a catalogue of 69 products, **When** a visitor opens `/`, **Then** 12 are listed with name,
   category, price and "In stock" or "Out of stock", and a pager reads "Page 1 of 6".
2. **Given** page 1, **When** the visitor clicks Next, **Then** the URL gains `page=2` and the next 12
   appear.
3. **Given** a visitor who is not signed in, **When** they browse, **Then** nothing asks them to sign
   in.

### User Story 2 - A visitor narrows the list and can share it (Priority: P1)

**Why this priority**: equal first; #23 names search as "the first thing a visitor does".

**Independent Test**: search "lap", choose a category, sort by price high to low; copy the URL into a
new tab - the same list appears.

**Acceptance Scenarios**:

1. **Given** a search term, **When** it is submitted, **Then** products whose name or SKU contains it
   (case-insensitive) are listed, and the count says what was searched for.
2. **Given** a category and a sort order, **When** chosen, **Then** the list changes and the URL holds
   `category` and `sort`.
3. **Given** a new search, filter or sort, **When** applied, **Then** the list starts again at page 1.
4. **Given** a URL with `q`, `category`, `sort` and `page`, **When** it is opened or reloaded, **Then**
   the same list appears.
5. **Given** nothing matches, **When** searched, **Then** the page says "Nothing matches."

### User Story 3 - A visitor opens one product (Priority: P2)

**Why this priority**: second, because a card already shows the essentials; the page adds the
description and the tax note.

**Independent Test**: click a card; the product page shows its name, price, availability, description
and SKU. Open `/products/<random guid>`: "This product does not exist."

**Acceptance Scenarios**:

1. **Given** a product, **When** its page opens, **Then** it shows name, price, availability,
   description and SKU, and says the price excludes tax, added at checkout for the delivery country.
2. **Given** an id Catalog does not know, **When** the page opens, **Then** it says the product does not
   exist (404), not that loading failed.
3. **Given** any other failure, **When** the page opens, **Then** it says the product could not be
   loaded.

### Edge Cases

- **No product has an image** (nothing in Catalog stores one): a placeholder tile with the name's first
  letter; filed as #45.
- **Availability is never a count** - Catalog holds only a read model of Inventory's stock (specs/004).
- **Inactive products are not filtered from the listing** - but nothing can make a product inactive
  yet (see "What building it found").
- **The catalogue cannot be reached**: the listing says "The catalogue could not be loaded. Is the
  backend running?"; the category list falls back to empty.
- **A stale answer** (the id in the URL changed while a request was in flight): the page ignores an
  answer for a different id.

## Requirements

- **FR-001**: The home page lists products, 12 to a page, with name, category, price and availability.
- **FR-002**: A shopper can search by name or SKU, filter by category, and sort by name or price.
- **FR-003**: Search, filter, sort and page live in the URL, so a result can be shared or reloaded.
- **FR-004**: A product page shows the name, price, availability, description and SKU. An unknown id says the product does not exist.
- **FR-005**: Availability shows only "In stock" / "Out of stock", never a count, because Catalog only has a read model (specs/004).
- **FR-006**: The price is labelled as excluding tax, which is added at checkout for the delivery country (ADR-002).
- **FR-007**: Browsing MUST NOT require an account; every call is anonymous.

### Key Entities

- **Product (as shown)** - id, name, description, price, availability, SKU, category id.
- **Category (as shown)** - id and name, for the filter and the card's label.
- **Page of products** - items, page number, total pages, total count, has previous, has next.

## Success Criteria

- **SC-001**: A search, filter, sort and page combination survives a reload and a copied URL unchanged.
- **SC-002**: An unknown product id shows "This product does not exist." in 100% of cases, distinct from
  a failed load.
- **SC-003**: No stock count appears anywhere in the storefront.
- **SC-004**: The pages build and lint clean in CI.

## What building it found

- **No product has an image.** Nothing in Catalog stores one. Filed as
  [#45](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/45); the page shows a
  placeholder.
- **The listing does not filter out inactive products.** This cannot cause a problem today, because
  nothing sets `IsActive = false` (there is no deactivate command), and checkout refuses an unsellable
  product over gRPC anyway. Recorded here, not filed, because it cannot be reproduced.
- **No backend change was needed.** Paging, search and sorting already existed on `GET /api/products`.

## Assumptions

- `GET /api/products` already pages, searches name and SKU, filters by category and sorts by name or
  price; `GET /api/categories` lists every category (no paging).
- One currency and English only; the price is formatted, never computed with.

## Out of Scope

- Images (#45), faceted filters, translation, currencies - later features (specs/019, 021, 022).
- Adding to a cart - #37 (specs/017).
