# Feature Specification: A shopper saves a product for later

**Feature Branch**: `075-saved-products` | **Created**: 2026-09-26 | **Issue**: #109 (closes it)

## Why

A shopper who is not ready to buy has nowhere to keep a product except the cart, which holds a quantity, reserves
nothing, and is where things go to be bought.

## User Scenarios

### US1 - Save and unsave (P1)

A signed-in shopper taps a heart, on a product card or on the product page, to save the product; tapping it again
unsaves it.
- Saving twice keeps one entry.
- Unsaving something not saved is no error.
- Only a product on sale can be saved. Anything else is the public lookup's 404, so a hidden product's id is not
  confirmed.

### US2 - The saved list (P1)

`/saved` lists what the shopper saved, newest first, with each product's current price in the shopper's currency
and whether it is in stock.
- A product that was withdrawn or taken down since stays in the list, reading as unavailable rather than causing
  an error.
- A product deleted outright leaves the list.

### US3 - Back in stock (P2)

When a saved product comes back in stock, which is Catalog's rollup flipping from out of stock to in stock, each
shopper who saved it is told once, with a link to it. There is one notice per flip, not one per announcement.

## Requirements

- **FR-001** `saved_products (CustomerId, ProductId, SavedAt)`, with the key `(CustomerId, ProductId)` and a
  cascade from products. Saving is `INSERT ... ON CONFLICT DO NOTHING`.
- **FR-002** The endpoints, all for the caller, whose id comes from the token:
  - `PUT /api/products/{id}/saved`, which answers 204;
  - `DELETE /api/products/{id}/saved`, which answers 204;
  - `GET /api/products/saved?page=`: a page, newest first;
  - `GET /api/products/saved/ids`: the ids, for drawing hearts on a page of cards.
- **FR-003** `SavedBackInStock {product}` is declared in `notification-kinds.json` and worded in the storefront.
- **FR-004** Storefront:
  - a heart on the product card and on the product page (signed-out shoppers are sent to sign in);
  - the `/saved` page, linked from the account menu;
  - Vitest tests.
- **FR-005** Bruno: save, save again, list, list the ids, unsave, and without a token (401).

## Out of scope

- A "price dropped" notice.
- Sharing a list.
