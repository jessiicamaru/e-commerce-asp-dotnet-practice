# Research: A shop has a page

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #197

---

## D1 - The shop read lives in Catalog, from its read model

**Decision**: Catalog serves `GET /api/shops/{sellerId}` from its `sellers` read model (name, description, suspension),
plus a count of the seller's products on the shelf. Identity stays the source of truth for the description and announces
it with `SellerDescribedEvent`, the same way it announces the name with `SellerRenamedEvent` (specs/027).

**Rationale**: specs/027 put the shop name in Catalog so that an anonymous page of products costs no call to Identity and
keeps working with Identity down. The shop page is exactly that kind of page: a name above a grid of Catalog's products.
The same reason applies, and the read model already exists. The copy can be seconds behind, which costs a slightly old
page. The project accepts that for display (specs/031's line: display may be eventually consistent, authorization may
not).

**Alternatives considered**:

- **Identity serves the shop and Catalog the products.** Rejected: two services behind one page, plus an anonymous
  endpoint on Identity that reads a seller profile by id. That is a new public surface on the service holding personal
  data.
- **The storefront takes the shop name from the first product.** Rejected: a shop with nothing on the shelf would have no
  name, and the description would have nowhere to come from.

---

## D2 - No page for the shop itself

**Decision**: products with no seller (the shop's own) keep "Sold by the shop" as plain text. The shop itself gets no
`/shops/` page.

**Rationale**: the shop's own goods are a curation across categories, not a seller with a storefront, and the catalogue is
already their page. A page for them would need a pseudo-id, since `null` is not an address, and would duplicate the home
page. I decided this on the user's behalf, under the session's standing instruction to follow the recommended option.

**Alternatives considered**: a reserved id such as `/shops/shop`. Rejected because that id would mean something no other
id does, and every reader of `sellerId` would then have to know about it.

---

## D3 - The description is not moderated

**Decision**: a seller sets or clears the description at will (at most 500 characters), and nothing sends it to review.
The storefront shows it as text, never as markup.

**Rationale**: the shop name is the more prominent of the two, and it is not moderated either (specs/027). specs/045
moderates products because a product is what is sold and paid for; a description sells nothing. Staff can still stop a
shop (specs/095), and a suspended seller's shop page is a 404, so its description goes with it.

**Alternatives considered**: sending the shop back to review when its description changes. Rejected: there is no
shop-level review state to send it to, and building one for a paragraph is out of proportion.

---

## D4 - The description has its own timestamp guard

**Decision**: `sellers.DescriptionObservedAt` guards the description. It is separate from `ObservedAt`, which guards the
name, and from the suspension's own guard. `TryRecordDescriptionAsync` is a single statement:
`INSERT ... ON CONFLICT DO UPDATE ... WHERE old IS NULL OR old < EXCLUDED`. If a description arrives before the
registration, the statement creates the row with an empty name and `DateTime.MinValue`, and the registration's own guarded
upsert fills them in later (the specs/095 pattern).

**Rationale**: the three facts change independently. With one shared timestamp, a newer rename could swallow an older
description that had not been recorded yet, or the other way round. Doing it in one statement makes a redelivery a no-op
and makes consumers on several instances safe without a lock.

**Alternatives considered**: reading the row and then writing it through EF. Rejected: two instances could both read
"older", and whichever committed last would win, whether or not its event was the newer one.

---

## D5 - The listing takes a `sellerId` instead of a second endpoint

**Decision**: `GetProductsQuery` gains a `SellerId`, which it passes to the existing
`GetPaginatedAsync(sellerId, listedOnly: true)`.

**Rationale**: the repository already filters by seller for the seller's own list, and the same call applies the public
shelf rule (`OnShelf`). The shop page therefore cannot drift from the catalogue: a product taken down disappears from
both. Search, category and sort work on the shop page with no extra code.

**Alternatives considered**: a dedicated `GET /api/shops/{id}/products`. Rejected: it would be a second implementation of
the shelf rule and the paging, and it could fall out of step with the catalogue.
