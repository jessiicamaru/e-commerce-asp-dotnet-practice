# Feature Specification: Filter the catalogue by price and by what is in stock

**Feature Branch**: `109-catalogue-filters` | **Created**: 2026-09-27 | **Issue**: #216

**Status**: Draft

**Input**: Issue #216 - "the catalogue cannot be filtered by price or by what is in stock".

## Why

`GetProductsQuery(PageNumber, PageSize, CategoryId, SearchTerm, SortBy, SellerId)` has no price and no availability.
So a shopper cannot ask for "cameras under 20,000,000 ₫" or "only what I can buy now". With a real catalogue, paging
through everything to find what fits a budget is the whole experience.

## User Scenarios & Testing *(mandatory)*

### US1 - A price range (Priority: P1)

A shopper sets a minimum and/or maximum on the catalogue, in the currency they are browsing in. Only products whose
"from" price falls in that range are listed. The range is in the address, so the page can be shared and reloaded.

**Why this priority**: It is the filter shoppers reach for first.

**Acceptance Scenarios**:

1. **Given** `maxPrice=20000000` in VND, **Then** no listed product's "from" price is over 20,000,000.
2. **Given** `minPrice=20000000`, **Then** no listed product's "from" price is under it.
3. **Given** a price bound in USD, **Then** a product with no USD price is excluded, and nothing is converted.
4. **Given** `minPrice` greater than `maxPrice`, or a negative bound, **Then** 400.

---

### US2 - Only what is in stock (Priority: P1)

The shopper ticks "In stock only", and only products they can buy now are listed.

**Acceptance Scenarios**:

1. **Given** `inStock=true`, **Then** no product whose availability is out of stock is listed.
2. **Given** `inStock` absent or false, **Then** the listing is as before, including products that are out of stock.

### Edge Cases

- **The "from" price** is the cheapest *active* variant's price in the asked currency, the same number the card shows
  and the price sort uses. A product with an in-range variant but a cheaper out-of-range one is judged by its "from"
  price.
- **No price in the asked currency** (specs/022): excluded whenever a bound is given, never converted.
- **The filters combine** with category, search, seller and sort, using AND.
- **Availability is the read model** (seconds behind Inventory). It is a display filter, and nothing sells against it,
  as today.
- **A bound of 0** is a real bound, not "none".

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `GET /api/products` accepts `minPrice`, `maxPrice` (decimals, ≥ 0, `minPrice ≤ maxPrice` when both are
  given) and `inStock` (bool).
- **FR-002**: A price bound is compared with the product's "from" price in the request's currency:
  - in the default currency, `products.Price`;
  - otherwise, the minimum active variant price in `variant_prices` for that currency.
  - A product without one is excluded.
- **FR-003**: `inStock=true` keeps only `products.Availability = true`.
- **FR-004**: The query uses indexes. Asked for its plan with sequential scans priced out, a price-bounded listing uses
  an index on the price, and has no per-product subplan.
- **FR-005**: The catalogue page:
  - a minimum and a maximum in the browsing currency, applied together;
  - an "In stock only" switch;
  - both kept in the URL like the category and the sort, and cleared with the rest.

## Success Criteria *(mandatory)*

- **SC-001**: With `maxPrice=20000000&currency=VND`, nothing over it is listed. With `inStock=true`, nothing out of
  stock is. Both are tested against PostgreSQL.
- **SC-002**: The plan test in FR-004 passes for both currencies.

## Assumptions

- There are no price buckets or histogram. The shopper types the numbers.
- The bounds are whole or decimal amounts in the browsing currency. The storefront does not round them; the server
  compares them as given.
