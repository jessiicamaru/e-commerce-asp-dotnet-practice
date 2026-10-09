# Research: A cart before signing in

## D1 - The browser keeps the lines; the server prices them

**Decision**: the guest cart is `{ productId, variantId, quantity }` lines in `localStorage`. Reading it posts the lines to
an anonymous `POST /api/cart/price`, which prices them with **the same code** as the stored cart
(`CartPricing.PriceAsync`, extracted from `GetMyCartQueryHandler`) and stores nothing.

**Rationale**: the statuses (not for sale, no longer available, not sold in this currency, prices unavailable) and the
estimate are decided in one place, so a guest cart and an account cart cannot read differently. No ownerless cart is
ever stored: nothing to expire, nothing to steal by guessing an id.

**Rejected**:
- *An anonymous server cart keyed by a browser id* - a row with no owner, an id that must be unguessable, a sweeper to
  expire it, and a second identity next to the token.
- *Pricing in the browser from the product reads* - a second implementation of the line statuses and the estimate,
  which is exactly how the quote and the order were kept from disagreeing (one `CheckoutPricing`).

## D2 - The merge takes the larger quantity, in one transaction

**Decision**: `POST /api/cart/merge` (signed in) with the browser's lines; under the cart's row lock, each line is
added when absent and raised to the guest quantity when the account holds fewer; one save. Then the browser empties its
cart.

**Rationale**: "larger" is idempotent - a retry after a lost response, or two tabs signing in at once, cannot double a
quantity - without a table of merges already applied. Summing would need that table (and its erasure on account
deletion, specs/112) to be safe.

**Rejected**: summing quantities with a merge id recorded per merge - correct, but a new table, a new personal-data
declaration and an erasure, for a case ("I wanted 4, not 2") the shopper can fix with one click.

## D3 - Merge where every sign-in ends

**Decision**: one storefront component watches the signed-in user and, when there is one and the browser holds lines,
merges and then empties - whether the session came from the password, a two-factor code, registration or a restored
session on load.

**Rationale**: four entry points, one rule. A merge per form would be missed by the next way of signing in.

## D4 - Limits

**Decision**: at most 50 lines, quantities 1-999, for both requests; the browser stops adding at 50 lines.

**Rationale**: the anonymous pricing asks Catalog once per request; a bound keeps one request from becoming a
catalogue-sized gRPC call.

## D5 - Storage failing is not an error

**Decision**: every read and write of `localStorage` is wrapped; when it fails, adding signed out sends the shopper to
sign in as before (specs/126).
