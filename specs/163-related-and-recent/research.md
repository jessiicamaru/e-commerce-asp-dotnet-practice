# Research: Related products and recently viewed

## D1 - Related is the listing, asked twice

**Decision**: the handler asks `GetPaginatedAsync` for the product's category (a department includes its categories,
specs/158) sorted by `rating_desc`, drops the product, and - when that is short of the limit - asks again for the
category's department and appends what is new.

**Rationale**: the listing already decides "on the shelf", loads what a card needs (variants, prices, translations) and
is indexed. A second query path would be a second definition of a sellable card.

**Rejected**: ranking by shared specification options (specs/159) - better matches in theory, but most products have
few options set and the ranking would mostly be noise; co-purchase data does not exist.

## D2 - `rating_desc`, a new sort the listing can use too

**Decision**: `RatingCount` descending, then `RatingAverage` descending, then name.

**Rationale**: the count first - one five-star review should not outrank a hundred four-and-a-half-star ones.

## D3 - An off-shelf product answers an empty list

**Decision**: unknown and off-shelf are the same empty answer.

**Rationale**: a 404 for one and a list for the other would tell anybody which ids are real products (specs/081's
reasoning). The product page of an off-shelf product is its seller's or staff's view, where the row is not needed.

## D4 - Recently viewed lives in the browser and is read through the listing

**Decision**: `localStorage["recentlyViewed"]`, product ids only, most recent first, at most 12; read with the listing's
new `ids` filter.

**Rationale**: no account needed, nothing stored on the server about what somebody looked at (specs/111 would have to
declare and export it), and the listing's filter keeps off-shelf products out for free. The product page records the
view where it already records one for the counts (specs/086).

**Rejected**: a server-side history - personal data with an export, an erasure and a retention rule, for a convenience.
