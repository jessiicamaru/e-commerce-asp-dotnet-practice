# Research: A compare-at price per variant

## D1 - A column beside each price, not a table of its own

**Decision**: `product_variants.CompareAtPrice` for the default currency and `variant_prices.CompareAtAmount` for every
other - each beside the price it is compared against.

**Rationale**: a compare-at means nothing without its price. Beside it, a compare-at in a currency the variant is not
priced in cannot exist; removing a price takes its compare-at with it; a **CHECK** constraint (`CompareAt IS NULL OR
CompareAt > Price`) lets the database refuse a "reduction" that is not one; and every read that loads a variant's prices
already loads it, with no new `Include` in nine loaders. It mirrors where the price itself lives (specs/022 research
D2: the default currency on the variant, the rest in `variant_prices`).

**Rejected**: a table `variant_compare_at_prices (VariantId, Currency, Amount)`. One shape for every currency, but a row
could outlive its price, the "above the price" rule could only be checked in code, and nine loaders would need another
`Include`.

## D2 - A price raised to or above its compare-at clears it

**Decision**: the three writers of a price (`SetVariantPrice`, `UpdateProductVariant`, and the price of a variant row)
clear the compare-at in the same change when the new price is not below it.

**Rationale**: the alternative - refusing the price change - makes a seller clear one field before they may change
another, and a stale compare-at shown struck through below the price would be a false claim. The CHECK makes forgetting
this impossible to miss: the save fails.

## D3 - Its own endpoint

**Decision**: `PUT /api/products/{id}/variants/{variantId}/prices/{currency}/compare-at` `{ amount }` and `DELETE` on the
same address.

**Rationale**: an optional `compareAt` on the price's own `PUT` would make every existing caller (the seed, Bruno, the
seller's page) clear it by omission. A separate address leaves the price endpoint exactly as it was.

## D4 - The card shows the compare-at of the variant that gives the "from" price

**Decision**: `ProductResponse.CompareAtPrice` is the compare-at of the cheapest active variant priced in the currency
(the one whose price is shown); null when it has none.

**Rationale**: the card's struck-through price must be about the price beside it. "The highest compare-at of any
variant" would put a reduction on a price that is not reduced.

## D5 - Display only, and never a reason for review

**Decision**: nothing that charges reads the compare-at; setting it never calls `ProductReview.AfterSellerEditAsync`.

**Rationale**: the issue's rule - the price charged does not change - and specs/045's decision that prices do not send a
product to review. A false reduction is a matter for reports (specs/101) and staff, like a false description.

## D6 - "On sale" in SQL

**Decision**: `onSale=true` keeps products with an active variant whose compare-at in the asked currency is set: the
variant's column for the default currency, its `variant_prices` row's column for another.

**Rationale**: the CHECK guarantees a set compare-at is above its price, so "set" is "on sale" - no comparison needed in
the query.
