# Quickstart: Browse, Search and Open a Product

> Written on 2026-09-27, after the feature merged (#46), from the code at that merge, the pull request
> and docs/features/catalog.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server && docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
cd ../client && npm ci && npm run dev          # http://localhost:5173
```

A catalogue with products in it (the pull request ran against 69; today `server/seed/seed-catalogue.py`
gives 14 cameras - specs/023).

## Scenario 1 - The calls the pages make, through the proxy (FR-001, FR-002, FR-004)

```bash
P=http://localhost:5173/api
curl -s "$P/products?pageSize=2&sortBy=price_desc" | jq '[.items[] | {name, price}]'
curl -s "$P/products?pageSize=2&pageNumber=2"      | jq '{pageNumber, totalPages, totalCount}'
curl -s "$P/products?searchTerm=lap"               | jq '[.items[].name]'
curl -s -o /dev/null -w '%{http_code}\n' "$P/categories"
curl -s -o /dev/null -w '%{http_code}\n' "$P/products/$(uuidgen)"
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5173/products/abc
```

**Expected**, as the pull request recorded it against its data:

```text
/api/products?pageSize=2&sortBy=price_desc   -> Expensive Laptop 40,000,000 first, then 120,000
/api/products?pageSize=2&pageNumber=2        -> page 2 / 35, count 69
/api/products?searchTerm=lap                 -> 1: Expensive Laptop
/api/categories                              -> 200
/api/products/<unknown id>                   -> 404
/products/abc (SPA route)                    -> 200 (index.html)
```

Against today's data the names and counts differ; the shape and the statuses are what to check. (The
listing's parameters have grown since - language, currency, seller - without changing these.)

## Scenario 2 - In the browser (US1-US3, SC-001-SC-003)

1. Open <http://localhost:5173/> signed out: 12 cards, each with a placeholder letter, a category, a
   price and "In stock" / "Out of stock". No number of units anywhere.
2. Search `lap`, pick a category, sort "Price, high to low": the URL holds `q`, `category`, `sort`.
3. Click Next, then reload: the same page. Copy the URL into a new tab: the same list.
4. Change the sort: back to page 1.
5. Open a card: name, price, "Price excludes tax, which is added at checkout for your delivery
   country.", availability, description, SKU.
6. Open `/products/00000000-0000-0000-0000-000000000000`: "This product does not exist."

**Not run at the merge**: the pull request states the pages were type-checked and built and the calls
checked with curl, but nobody clicked through them.

## Scenario 3 - Build (SC-004)

```bash
cd client && npm run lint && npm run build
```

**Expected**: no warnings, build succeeds (as recorded).
