# Feature Specification: A shopper saves a product for later

> Completed on 2026-09-27, after the feature merged (#159), from the code at that merge, the pull request and docs/features/saved-products.md.

**Feature Branch**: `075-saved-products` | **Created**: 2026-09-26 | **Issue**: #109 (closes it)

**Status**: Implemented - merged 2026-09-26 in PR #159

**Input**: Issue #109, "a shopper cannot save a product for later": save and unsave a product (the caller's own
list, from the token), a page listing them with current price and availability, a heart on the product card and
page, and optionally "back in stock" and "price dropped" notifications. Acceptance: saving twice keeps one entry;
a product withdrawn or taken down reads as unavailable, not as an error.

## Why

A shopper who is not ready to buy has nowhere to keep a product except the cart, which holds a quantity, reserves
nothing, and is where things go to be bought.

When the issue was filed there was nothing of the kind anywhere in the server: a search for `Wishlist`,
`Favourite` or `Favorite` across `server/src` found no file (issue #109).

## User Scenarios & Testing *(mandatory)*

### US1 - Save and unsave (P1)

A signed-in shopper taps a heart, on a product card or on the product page, to save the product; tapping it again
unsaves it.
- Saving twice keeps one entry.
- Unsaving something not saved is no error.
- Only a product on sale can be saved. Anything else is the public lookup's 404, so a hidden product's id is not
  confirmed.

**Why this priority**: It is the feature. Without saving there is no list to read and nobody to tell when a
product comes back; and it is the part the issue's acceptance names first ("saving twice keeps one entry").

**Independent Test**: As a customer, save a product twice, read the saved ids, unsave it twice. The ids hold the
product once after the saves and not at all after the unsaves, and every write answered 204.

**Acceptance Scenarios**:

1. **Given** a product on sale that the shopper has not saved, **When** they save it, **Then** it is on their
   list, once.
2. **Given** a product already saved, **When** they save it again - or twenty saves arrive at once - **Then** the
   list still holds it once, with the time it was first saved.
3. **Given** a product saved, **When** they unsave it, **Then** it leaves the list; **When** they unsave it again,
   or unsave a product they never saved, **Then** nothing changes and no error is shown.
4. **Given** a product waiting for review, rejected, taken down or inactive, **When** a shopper tries to save it,
   **Then** they get the same "not found" as for an id that does not exist.
5. **Given** a visitor who is not signed in, **When** they tap a heart, **Then** they are sent to sign in and back
   to the page they were on, and nothing is saved.

---

### US2 - The saved list (P1)

`/saved` lists what the shopper saved, newest first, with each product's current price in the shopper's currency
and whether it is in stock.
- A product that was withdrawn or taken down since stays in the list, reading as unavailable rather than causing
  an error.
- A product deleted outright leaves the list.

**Why this priority**: Equal first with US1 - a list nobody can read back is not a list. It also carries the
issue's second acceptance criterion: a product taken off sale reads as unavailable, not as an error.

**Independent Test**: As one customer save two products a moment apart, as another save one of them; read the
first customer's list. It holds only their two, the later first, each in the listing's words and today's price,
each saying whether it can be bought.

**Acceptance Scenarios**:

1. **Given** two products saved in turn, **When** the shopper opens their list, **Then** the one saved later is
   first.
2. **Given** another shopper saved a product too, **When** this shopper opens their list, **Then** they see only
   their own.
3. **Given** a saved product whose price changed since, **When** the list is read, **Then** it shows today's price
   in the currency the shopper browses in, and the name in their language.
4. **Given** a saved product that was since taken down, **When** the list is read, **Then** it is still there,
   marked as no longer available.
5. **Given** a saved product that was since deleted, **When** the list is read, **Then** it is simply gone, and
   the list reads without error.
6. **Given** a shopper who saved nothing, **When** they open `/saved`, **Then** they are told how to save something.

---

### US3 - Back in stock (P2)

When a saved product comes back in stock, which is Catalog's rollup flipping from out of stock to in stock, each
shopper who saved it is told once, with a link to it. There is one notice per flip, not one per announcement.

**Why this priority**: The issue calls it optional. It makes the list worth keeping - the shopper who could not
buy learns they now can - but saving and reading the list stand on their own without it.

**Independent Test**: Two shoppers save a product with no stock. Stock it, stock it again, sell it out, stock it
again. Each shopper holds two back-in-stock notices, each linking to the product.

**Acceptance Scenarios**:

1. **Given** a saved product out of stock, **When** it comes back in stock, **Then** each shopper who saved it is
   told once, with a link to the product.
2. **Given** a saved product already in stock, **When** more stock arrives, **Then** nobody is told again.
3. **Given** a product that goes out of stock and comes back twice, **When** it does, **Then** each saver is told
   twice - once per return.
4. **Given** a saved product that is off the shelf (not approved or not active), **When** it comes back in stock,
   **Then** nobody is told: there is nothing they could buy.

---

### Edge Cases

- **Saving the same product many times at once.** Twenty concurrent saves keep one entry; none of them fails
  (`Saving_twice_or_twenty_times_at_once_keeps_one_entry`).
- **Unsaving a product id that does not exist.** No error; nothing happens.
- **A made-up product id.** Saving it is the same 404 as a hidden product.
- **A product taken down, rejected or withdrawn after it was saved.** Stays on the list, unavailable. It is still
  counted in the ids, so its heart stays filled wherever the shopper can still see it.
- **A product deleted after it was saved.** Leaves the list with its row (cascade); no dangling entry.
- **Another variant of an in-stock product coming back.** The product was already buyable, so nobody is told.
- **An announcement redelivered, or overtaken by a newer one.** The variant's time guard (specs/054) ignores it,
  so it cannot produce a second notice.
- **Two availability updates for one product at once.** Each statement reads the value it started from, so both
  cannot report the flip (research D4).
- **A product that becomes available through a price change, an added variant or a reactivated variant.** Tells
  nobody - only Inventory's announcements report the flip (see [plan.md](./plan.md), "What this feature does not
  finish").
- **A shopper with many saved products that come back together.** One notice each; there is no digest.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** `saved_products (CustomerId, ProductId, SavedAt)`, with the key `(CustomerId, ProductId)` and a
  cascade from products. Saving is `INSERT ... ON CONFLICT DO NOTHING`.
- **FR-002** The endpoints, all for the caller, whose id comes from the token:
  - `PUT /api/products/{id}/saved`, which answers 204;
  - `DELETE /api/products/{id}/saved`, which answers 204;
  - `GET /api/products/saved?page=`: a page, newest first;
  - `GET /api/products/saved/ids`: the ids, for drawing hearts on a page of cards.

  (Completed on 2026-09-27: the list also takes `pageSize`, 1 to 50, default 12; `page` defaults to 1 and must be
  at least 1. Out of range is 400. See [contracts/http-api.md](./contracts/http-api.md).)
- **FR-003** `SavedBackInStock {product}` is declared in `notification-kinds.json` and worded in the storefront.
- **FR-004** Storefront:
  - a heart on the product card and on the product page (signed-out shoppers are sent to sign in);
  - the `/saved` page, linked from the account menu;
  - Vitest tests.
- **FR-005** Bruno: save, save again, list, list the ids, unsave, and without a token (401).
- **FR-006** Saving MUST accept only a product on sale - approved and active - and MUST answer anything else,
  including an id that does not exist, with the same 404 (`Product not found.`), so a hidden product is not
  confirmed.
- **FR-007** Each saved item MUST read the product as it is now, in the request's language and currency, in the
  listing's own response shape, with when it was saved and whether it can be bought (on sale, active, in stock).
- **FR-008** A product withdrawn, rejected or taken down after it was saved MUST stay in the list as unavailable;
  a product deleted outright MUST leave it.
- **FR-009** A back-in-stock notice MUST be sent only when the product's availability rollup turns from false to
  true in the statement that recomputes it, only for a product listed and active at that moment, and once per
  saver per such turn. It MUST go through the outbox in the same transaction as the availability it announces.
- **FR-010** No endpoint MAY take a shopper id; nobody can read or change another shopper's list.
- **FR-011** The heart MUST sit beside a card's link, not inside it, and MUST expose its state with
  `aria-pressed`.

### Key Entities

- **Saved product**: one shopper's note that they want to come back to one product. Who (the token's subject),
  which product, and when it was first saved. One per shopper per product; it goes when the product is deleted.
  It holds no price, no quantity and no copy of the product - those are read from the product every time.
- **Back-in-stock notice**: an ordinary in-app notification (specs/042) of kind `SavedBackInStock`, carrying the
  product's name and a link to it, sent to each saver on the product's return to stock.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Saving a product any number of times, including twenty at once, leaves exactly one entry - verified
  by `Saving_twice_or_twenty_times_at_once_keeps_one_entry` and Bruno 62-63.
- **SC-002**: 100% of save attempts on a product not on sale are refused with the same answer as an unknown id -
  verified by `Only_a_product_on_sale_can_be_saved`.
- **SC-003**: A product taken off sale after being saved reads as unavailable and never as an error; a deleted one
  is absent - verified by `A_product_taken_down_since_stays_and_reads_as_unavailable_and_a_deleted_one_goes`.
- **SC-004**: Two savers and two returns to stock, with a "still in stock" announcement between, produce exactly
  four notices, each linking to the product - verified by `Coming_back_in_stock_tells_whoever_saved_it_once_per_flip`.
- **SC-005**: A product off the shelf coming back produces zero notices - verified by
  `A_product_off_the_shelf_coming_back_in_stock_tells_nobody`.
- **SC-006**: A shopper's list contains only their own saved products, newest first - verified by
  `The_list_is_the_callers_own_newest_first_in_the_listings_words`.
- **SC-007**: Each of the four mutations named in PR #159 turns at least one of the above tests red.

## Assumptions

- Saving is per **product**, not per variant: the shopper saves the camera, then chooses a kit when buying.
- A signed-in account of any role can save; the list is always the caller's own.
- The product's name in the notice is its own (default-language) name: Catalog does not know the saver's
  language, and a notice stores data, never a sentence (specs/042).
- Availability is Catalog's read model of Inventory's stock (specs/004); a back-in-stock notice is therefore
  seconds behind Inventory by design, and it promises nothing - the stock may sell out before the shopper arrives.
- One notice per product per flip is acceptable; no digest.

## Out of scope

- A "price dropped" notice.
- Sharing a list.

Also not built, though the record does not say it was decided (added on 2026-09-27 from the code):

- saving a specific variant, notes on a saved product, or more than one list;
- a staff view of what shoppers saved.
