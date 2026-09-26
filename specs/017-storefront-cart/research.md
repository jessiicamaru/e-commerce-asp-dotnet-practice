# Research: A Cart and an Address Book

> Written on 2026-09-27, after the feature merged (#47), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

D1 and D2 are the two decisions the spec recorded; D3 and D4 are how the pages carried them out.

## D1 - The cart is never kept in the browser

**Decision**: adding needs an account. A signed-out visitor sees "Sign in to add this to your cart",
and the link carries the product's path so sign-in returns there.

**Rationale**: "A cart for signed-out visitors would need merging into the customer's cart at sign-in.
That is a backend feature (Cart has no anonymous cart)." Keeping one in the browser would be the client
working around what the backend lacks - the thing #34 says not to do.

**Alternatives considered**: a `localStorage` cart merged at sign-in. Rejected for the reason above.

## D2 - Send every change at once, then read the cart again

**Decision**: add, set quantity and remove each call the server and then re-read `GET /api/cart`; the
page never adjusts its own copy.

**Rationale**: "Names and prices come from Catalog when the cart is read, so the page does not
calculate totals itself." The cart stores no price (specs/010), so any figure the client computed
would be a second opinion about a price it does not own.

**Alternatives considered**: optimistic local updates. Not recorded as considered; they would require
the client to compute line totals.

## D3 - Line status in the customer's words

**Decision**: `lineProblem(status)` maps Cart's `Available`, `NotForSale`, `NoLongerAvailable`,
`PriceUnavailable` to a sentence (or none), with a fallback for anything else. `pricesAvailable: false`
gets its own message and the lines stay.

**Rationale**: FR-004 - "using Cart's own `status`". An unknown status still reads as "cannot be bought
right now" rather than disappearing.

**Alternatives considered**: not recorded.

## D4 - Identity's validation, next to the field

**Decision**: the address form shows `ApiError.fieldErrors` beside each input; the client adds no
validation of its own.

**Rationale**: Identity already validates addresses (postal code, two-letter country); the call layer
from specs/014 already flattens ProblemDetails `errors` per field. Doing it again in the client would
be a second rule that could drift.

**Alternatives considered**: not recorded.
