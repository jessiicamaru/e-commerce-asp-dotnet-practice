# Research: A shopper saves a product for later

> Written on 2026-09-27, after the feature merged (#159), from the code at that merge, the pull request and docs/features/saved-products.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-26 | **Issue**: #109

Eight decisions. D1 to D3 are the ones [plan.md](./plan.md) recorded inline when the feature was built, and are
carried here unchanged in substance. D4 to D8 are decisions the pull request, the code and
[docs/features/saved-products.md](../../docs/features/saved-products.md) make without naming them as decisions;
they are written down here so the next change does not undo one by accident. Where the record names no rejected
alternative, this file says "not recorded" rather than inventing one.

---

## D1 - Catalog owns the saved list

**Decision**: `saved_products` lives in Catalog's database, next to the products it names, and Catalog serves all
four endpoints.

**Rationale**: A saved product is about products: price, availability and listing status are all Catalog's. The
list reads each product the way the listing does - in the request's language and currency, with its "from" price,
its shop name and its availability rollup - so keeping it beside `products` means a page of saved products costs
no call to another service. The back-in-stock notice (D3) is decided by Catalog's own availability rollup, so the
list and the event that feeds it are in the same database and the same transaction.

**Alternatives considered**:

- **Keep it in Cart.** Rejected: the cart is another thing - it holds a quantity and is where things go to be
  bought. A saved product is something the shopper has not decided about yet (spec "Why").
- Any other home (Identity, a new service) - not recorded.

---

## D2 - A hidden product cannot be saved, but one saved before it was hidden stays

**Decision**: `PUT /api/products/{id}/saved` accepts only a product that is on sale - `Product.IsListed`
(approved, specs/045) and `IsActive` - and answers anything else with the public lookup's 404, `Product not
found.`. A product withdrawn, rejected or taken down **after** it was saved stays in the list with
`available: false`. A product deleted outright leaves the list, because the row cascades from `products`.

**Rationale**: Saving is asking about a public product, so it follows the public lookup's rule: a 403 or any
different error would confirm to anybody that a hidden product's id is real. The list, on the other hand, is the
shopper's own record of what they chose, and it says "no longer available" rather than silently dropping
something - "a list that silently loses things is one nobody trusts" (PR #159). A deleted product has nothing
left to show, so there the foreign key's cascade decides.

**Alternatives considered**:

- **Drop a product from the list when it leaves the shelf.** Rejected for the reason above: the shopper would
  find their list shorter with no explanation.
- **A 403 (or a distinct message) for a product that exists but is not on sale.** Rejected: it confirms the id to
  somebody who may not see the product (the rule specs/045 set for the public lookup).

---

## D3 - The back-in-stock notice is on the rollup's flip

**Decision**: A shopper who saved a product is told `SavedBackInStock` only when the product's availability
rollup (`products.Availability`, recomputed from its active variants) turns from false to true, and only when the
product is listed and active at that moment. One notice per saver per flip.

**Rationale**: Inventory announces availability per **variant**, and many times: an announcement can repeat the
current value, and the broker redelivers. Only the product going from none in stock to some in stock is news to
somebody who saved it. A repeated "still in stock" tells nobody, and neither does a product that is off the shelf
- the shopper could not buy it. The test
`Coming_back_in_stock_tells_whoever_saved_it_once_per_flip` sends in, still-in, out, in for two savers and
expects four notices.

**Alternatives considered**:

- **A notice per availability announcement.** Rejected: a variant announced available twice, or a second variant
  becoming available while the first already was, would tell the saver again about something that did not change
  for them.
- **A notice per variant coming back.** Rejected: the shopper saved the product, not a variant, and the product
  was already buyable if another variant was in stock.
- **Notifying for a product that is not on sale.** Rejected, and held by a mutation check: "Notifying for a
  product off the shelf" turns `A_product_off_the_shelf_coming_back_in_stock_tells_nobody` red.

---

## D4 - The flip is read inside the one statement that recomputes the rollup

**Decision**: `RecomputeProductRollupAsync` changes from `Task` to `Task<bool>`. Its single `UPDATE products`
gains a CTE, `WITH before AS (SELECT "Availability" AS was FROM products WHERE "Id" = @id)`, and a
`RETURNING (SELECT was FROM before) AS "Was", p."Availability" AS "Now"`. It returns true only when exactly one
row came back with `Was = false` and `Now = true`.

**Rationale**: Every part of one statement sees the same snapshot, so the CTE reads the value this statement
started from, and "came back in stock" is this statement's own flip. Nothing is read into memory and compared
afterwards. The rollup was already one statement (the "from" price and the availability are derived, so they are
computed where the variants are), and the flip is read in that same statement rather than in a second one.

> Corrected on 2026-09-27: plan.md said the CTE "reads the old value under `FOR UPDATE`". The code at the merge
> has no `FOR UPDATE` in the CTE; it is a plain `SELECT` that reads the statement's snapshot, and the row lock is
> the one the `UPDATE` itself takes. The docs page and the code comment describe it the same way as this
> decision.

**Alternatives considered**:

- **Read `Availability` in one statement, then run the `UPDATE`, and compare in C#.** Rejected (PR #159, docs
  rule 6): a concurrent write between the read and the update could let two statements both believe they flipped
  it, and each would send the notices. Held by a mutation check: "Ignoring 'was' in the flip" turns
  `Coming_back_in_stock_tells_whoever_saved_it_once_per_flip` red.
- **A separate "last notified" column or table per product.** Not recorded.

---

## D5 - Saving is one `INSERT ... ON CONFLICT DO NOTHING` on the key

**Decision**: The key of `saved_products` is `(CustomerId, ProductId)`, and `SavedProductRepository.SaveAsync`
is a single `INSERT INTO saved_products ... ON CONFLICT ("CustomerId", "ProductId") DO NOTHING`. Unsaving is a
guarded `DELETE` (`ExecuteDeleteAsync`) that affects zero rows when nothing was saved. A second save keeps the
first `SavedAt`.

**Rationale**: Constitution III asks for idempotency enforced by the database, not by code that checks first.
Saving twice - or twenty times at once - must keep one entry (the issue's acceptance). With the key and
`ON CONFLICT`, the database decides, and a race between two taps inserts once.

**Alternatives considered**:

- **A plain `INSERT` (or EF `Add` plus `SaveChangesAsync`).** Rejected: a second or concurrent save raises a
  unique violation. Held by a mutation check: removing `ON CONFLICT` turns
  `Saving_twice_or_twenty_times_at_once_keeps_one_entry` red.
- **Check whether the row exists, then insert.** Not recorded as considered.

---

## D6 - The saved list reads the product as it is now

**Decision**: Each item is the listing's own `ProductResponse`, built with `ProductResponse.From(...)` in the
request's language and currency and with the shop's name from Catalog's `sellers` read model, wrapped in
`SavedProductResponse(Product, SavedAt, Available)`. `Available` is `IsListed && IsActive && Availability`.
Nothing about the product is copied at the moment it was saved.

**Rationale**: This is not an order. The shopper wants today's price in their currency and whether they can buy it
now, and an order is the only place this project freezes words and prices (specs/009, specs/021). Reusing the
listing's response means the saved page and the product cards draw from one shape and the storefront reuses
`ProductCard`.

**Alternatives considered**: freezing the price or name at save time - rejected in the docs page's words ("this is
not an order"); any other alternative not recorded.

---

## D7 - A separate endpoint returns the ids alone

**Decision**: `GET /api/products/saved/ids` returns the caller's saved product ids (newest first), and the
storefront's `useSavedIds` asks for it once, shared by every heart on the page, with a `staleTime` of 60 s and
disabled while signed out.

**Rationale**: A page of product cards needs to know which hearts to fill. One request for the ids is enough to
draw every heart on the page (docs page, API table), and the public listing (`GET /api/products`) is left
unchanged.

**Alternatives considered**: a `saved` flag on every `ProductResponse` - not recorded as considered.

---

## D8 - The heart sits beside the card's link, and a signed-out tap goes to sign in

**Decision**: `SaveButton` is rendered on the product card inside a `relative` wrapper, absolutely positioned
over the card, **beside** the card's link rather than inside it; on the product page it sits beside the title.
Signed out, a tap navigates to `/sign-in` with `state.from` set to the current path, and asks the server nothing.

**Rationale**: A button inside a link is two controls in one and invalid HTML (the component's comment). Saving is
the shopper's, so it needs to know who they are (the component's comment), and the ids query is disabled while
signed out so a visitor does not ask and collect a 401 (the hook's comment).

**Alternatives considered**:

- **The heart inside the card's link.** Rejected: invalid HTML, and a tap would also follow the link.
- **Hiding the heart while signed out.** Not recorded as considered.

---

## Open risks

| Risk | Impact | Mitigation |
| :-- | :-- | :-- |
| Only the Inventory announcement path reports the flip | `SetVariantPrice`, `AddProductVariant` and `UpdateProductVariant` also call `RecomputeProductRollupAsync` and discard its result, so a product that becomes available because a variant was reactivated or added tells nobody; neither does a product approved or restored to the shelf while in stock | Not addressed by this feature; recorded in [plan.md](./plan.md) "What this feature does not finish" |
| One notice per product per flip | A shopper with many saved products that come back together gets one notice each | Accepted; the docs page lists "no digest" as a known limit |
| The notice names the product in its default language | The data carries `product.Name`, the product's own column, not a translation | Accepted; Catalog does not know the saver's language. Since specs/083 the email that goes with it is written in the reader's language by Identity, with the name still the default-language one |
