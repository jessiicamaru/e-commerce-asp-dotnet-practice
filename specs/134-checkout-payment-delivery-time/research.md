# Research: Checkout says how payment works and how long delivery takes

## D1 - Two numbers, not a sentence

**Decision**: `MinDays` and `MaxDays` (business days); the storefront words them in the reader's language.

**Rationale**: A stored sentence would read in one language only and could not be checked; two numbers are validated
and worded anywhere.

**Alternatives rejected**: A free-text "delivery time" per option - untranslatable and unvalidated; a single number -
cannot say "3-5".

## D2 - The stand-in line comes from Payment

**Decision**: The Payment card reads `GET /api/payment/health` and says "no money is moved" only when `provider` starts
with "Stub".

**Rationale**: `provider` is one of the three signals this project keeps so the stub is never mistaken for the real
thing. A sentence hard-coded into the storefront would go on saying "demonstration" after a real provider replaced the
stub - or, worse, a later edit would delete it while the stub was still there.

**Alternatives rejected**: Static text - outlives the truth in one direction or the other; a new Order field - Order
does not know which provider Payment uses.

## D3 - The estimate is not frozen on the order

**Decision**: Orders keep freezing the option's name and price, not its estimate.

**Rationale**: An estimate is a promise for the choice; once the order exists its parcels carry real dates (shipped,
delivered) and tracking. Freezing it would add two columns to `orders` to show a number nobody needs afterwards.

**Alternatives rejected**: Freezing it on `orders` - columns for a figure the order page does not show.

## D4 - Seeded only for new codes

**Decision**: `Shipping:Options:N:MinDays/MaxDays` are written by the existing seed, which inserts missing codes only.

**Rationale**: specs/098: a restart never undoes an edit. An existing installation's options get no estimate until an
administrator sets one - the same as a renamed option keeps its name.

**Alternatives rejected**: Backfilling stored rows from configuration in the migration - would overwrite nothing today,
but would make configuration a second opinion on stored rows.
