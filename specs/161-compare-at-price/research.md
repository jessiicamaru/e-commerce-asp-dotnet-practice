# Research: A compare-at price per variant

## D1 - A column beside each price, not a table of its own

**Decision**: `product_variants.CompareAtPrice` for the default currency and `variant_prices.CompareAtAmount` for every
other - each beside the price it is compared against.

**Rationale**: a compare-at means nothing without its price. Beside it, a compare-at in a currency the variant is not
priced in cannot exist; removing a price takes its compare-at with it; a **CHECK** constraint (`CompareAt IS NULL OR
CompareAt > Price`) lets the database refuse a "reduction" that is not one; and every read that loads a variant's prices
already loads it, with no new `Include` in nine loaders. It mirrors where the price itself lives (specs/022 research
D2: the default currency on the variant, the rest in `variant_prices`).

**Accepted risk - a rollback**: an image from before this does not know to clear a compare-at (D2), so while one is
running, raising a price to or above an existing compare-at is refused by the CHECK (a 500 from that image). Prices
can still be lowered, nothing is charged differently, and clearing the compare-at (the column set to null) unblocks it.
The CHECK is worth more than that window: without it a stale compare-at below its price would be shown as a reduction.

**Rejected**: a table `variant_compare_at_prices (VariantId, Currency, Amount)`. One shape for every currency, but a row
could outlive its price, the "above the price" rule could only be checked in code, and nine loaders would need another
`Include`.

## D2 - A price raised to or above its compare-at clears it

**Decision**: the writers of a price - `SetVariantPrice` (the variant's column and a `variant_prices` row) and `UpdateProductVariant` -
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

**Decision**: `onSale=true` keeps products whose card shows a reduction: the active variant giving the "from" price in the
asked currency has a compare-at set - the variant's column against `products.Price` (the default currency's rollup), or
its `variant_prices` row against the cheapest active row in another.

**Rationale**: the filter must mean what the card shows (D4). The first version kept any product with *some* reduced
active variant, and Bruno's run found the result: a product whose dearer shape was reduced was listed under "On sale"
with nothing struck through on its card. The CHECK guarantees a set compare-at is above its price, so "set" is
"reduced" - only the "which variant" needs comparing.

**Rejected**: keeping "any variant" and adding a "Sale" badge for a card whose cheapest shape is not reduced - a second
signal to explain, for a case a seller can avoid by reducing the shape the card shows.
