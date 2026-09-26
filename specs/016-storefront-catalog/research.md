# Research: Browse, Search and Open a Product

> Written on 2026-09-27, after the feature merged (#46), from the code at that merge, the pull request
> and docs/features/catalog.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

## D1 - The URL is the state of the listing

**Decision**: search, category, sort and page are query parameters (`q`, `category`, `sort`, `page`),
read with `useSearchParams`; changing any filter drops `page` so the list starts again at 1. The search
box keeps a draft and writes `q` only on submit.

**Rationale**: "driven by the URL so a search can be shared, bookmarked and survives a reload" (the
page's comment); FR-003.

**Alternatives considered**: component state. Rejected: a reload or a shared link would lose the
search.

## D2 - Use Catalog's listing as it is

**Decision**: no backend change. `pageSize=12`, `sortBy` one of `name_asc`, `name_desc`, `price_asc`,
`price_desc` (the default is name ascending), `searchTerm` and `categoryId` passed through.

**Rationale**: "Paging, search and sorting already existed on `GET /api/products`." #23 had expected a
listing with "a page number and a page size" and no search; by this merge search and sort were there.

**Alternatives considered**: not recorded.

## D3 - Availability as a label, never a number

**Decision**: `InStock` → "In stock", anything else → "Out of stock"; one component used by both pages.

**Rationale**: Catalog knows only whether a product is available - a read model fed by Inventory's
`StockAvailabilityChangedEvent` - and "nothing may sell against it" (specs/004). A number would be a
second, stale copy of Inventory's fact.

**Alternatives considered**: asking Inventory's public `GET /api/stock/{id}` for a count on the product
page. Not recorded as considered.

## D4 - Missing images are a placeholder and an issue, not a workaround

**Decision**: a tile with the product name's first letter, and issue #45 against Catalog.

**Rationale**: the storefront-wide rule on #34 - what the interface needs and the backend lacks is filed
as its own issue. Catalog stored no image at all.

**Alternatives considered**: stock photographs or generated images in the client. Not recorded as
considered; the rule above excludes them.
