# Feature Specification: The catalogue's public reads, served from memory

**Feature Branch**: `feature/361-catalogue-cache`
**Created**: 2026-10-08
**Status**: Draft
**Issue**: #361
**Input**: "Check whether the system still needs anything - a cache, for instance." Nothing caches: no `IMemoryCache`,
`HybridCache`, output cache or Redis in `server/src`, and no JSON response carries a cache header. Decided with the
user: an in-memory cache first; Redis only if it is needed later.

## Why this exists

Every anonymous read of the catalogue - the listing and its searches, a product page, the categories, a shop page -
goes to PostgreSQL, although the catalogue is written rarely compared with how often it is read. The storefront's
landing page alone asks for the listing and the categories on every visit. Product images are already cacheable for
good (their address changes with the image, specs/019); the JSON is not.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A shopper's catalogue page is answered from memory (Priority: P1)

An anonymous shopper browsing the listing, searching, opening a product, the categories or a shop page is answered
from memory when the same question was answered recently, in the same language and currency.

**Why this priority**: it is the traffic the shop has the most of, and the cheapest to serve.

**Independent Test**: two identical anonymous requests: the second does not reach the handler. The same request in
another language, currency or query does.

**Acceptance Scenarios**:

1. **Given** an anonymous `GET /api/products?...` answered once, **When** it is asked again within the expiry, **Then**
   it is answered from memory with the same body and the same `Content-Language`, `X-Currency` and `Vary` headers.
2. **Given** the same path in another negotiated language or currency, or another query string, **When** asked,
   **Then** it is a separate entry - never one shopper's Vietnamese or dong for another's English or dollars.
3. **Given** a signed-in request (a seller reading their own off-shelf product, staff), **When** asked, **Then** it is
   neither served from nor stored in the cache.
4. **Given** a 404 or any non-200 answer, **When** asked again, **Then** it is not served from the cache.

### User Story 2 - Nothing stale after a write (Priority: P1)

Whatever changes the catalogue - a seller's edit, a moderator's take-down, a price, Inventory's availability arriving
through the broker, a shop paused or renamed - the next anonymous read shows it.

**Why this priority**: a cache that shows a taken-down product or an old price is worse than no cache.

**Independent Test**: a write to a catalogue table in a committed transaction evicts; a rolled-back one does not; a write
only to a table the cached reads do not show (views, saved products, questions) does not.

**Acceptance Scenarios**:

1. **Given** a cached listing, **When** any committed statement writes `products`, `product_variants`, `variant_prices`,
   `product_translations`, `variant_options`, `variant_option_translations`, `categories`, `category_translations` or
   `sellers` - through EF, a guarded SQL statement or a consumer - **Then** the cache is emptied after the commit.
2. **Given** such a write in a transaction that rolls back, **When** it ends, **Then** nothing is evicted.
3. **Given** a product view being counted, **When** it is written, **Then** nothing is evicted (it would empty the cache
   on every page view).

### Edge Cases

- **Several Catalog instances**: eviction is per instance; another instance can serve an entry until it expires. The
  expiry (`Caching:CatalogueSeconds`, 30 by default) is that bound.
- **A read racing a write**: a read that started before a commit and finishes after its eviction can store the old
  answer; the expiry bounds it too.
- **Switched off**: `Caching:CatalogueSeconds=0` disables it without a release; out of range (negative, over 600) stops
  Catalog at start.
- **Nothing that decides is cached**: checkout prices over gRPC, stock comes from Inventory, ownership is asked live
  (specs/031) - all untouched.

## Requirements *(mandatory)*

- **FR-001**: Anonymous `GET` of the product listing, a product, the categories and a shop page are output-cached in
  memory, 200s only, varying by query, negotiated language and negotiated currency.
- **FR-002**: Requests carrying `Authorization` are never served from or stored in the cache.
- **FR-003**: Every committed write to the catalogue tables named in Story 2 evicts every cached entry; a rolled-back
  write evicts nothing; writes to other tables evict nothing.
- **FR-004**: The expiry is configured (`Caching:CatalogueSeconds`, default 30, 0 = off, 1-600 otherwise) and checked
  at start.
- **FR-005**: Product images keep their own caching (specs/019); the gateway and the storefront change nothing.

## Success Criteria *(mandatory)*

- **SC-001**: Tests show a second identical anonymous read not reaching the handler; language, currency and query
  keeping entries apart; a signed-in read bypassing; a non-200 not cached.
- **SC-002**: Tests against PostgreSQL show eviction after a committed catalogue write (EF and raw SQL), none after a
  rollback, none for a view.
- **SC-003**: `loadtest/run.sh browse` (anonymous, peak 50) over several warm runs before and after: lower median and
  p95 for the listing and search, the same zero errors.
- **SC-004**: Every Catalog test and the Bruno/verify scripts' expectations still hold (CI).

## Assumptions

- In memory (ASP.NET Core output caching's default store), decided with the user. A Redis store can replace it by
  registration alone.
- Writes are rare enough that evicting everything on any catalogue write costs less than tracking what each one
  touched - and cannot miss a dependency.
