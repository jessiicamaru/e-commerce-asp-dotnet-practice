# Research: Shoppers see the vouchers they could use

## D1 - A flag on the voucher, false by default

**Decision**: `vouchers.IsPublic boolean NOT NULL DEFAULT false`, set on create and by the specs/113 edit.

**Rationale**: the issue asks for "public (shown) or private (code only)". Private by default keeps every existing
voucher exactly as its owner made it - some were handed to one customer. The database default decides only the backfill
and an older image's inserts (which cannot set it): both private, which is the safe side, unlike specs/093's review
default where the safe side was the other one.

**Alternatives rejected**: a "channel" enum (shown on shop / product / checkout separately) - no owner has asked to
show a voucher in one place and hide it in another, and three booleans triple the tests.

## D2 - One anonymous endpoint, filtered by the caller's page

**Decision**: `GET /api/vouchers/public?platform=&sellerId=&productId=&variantId=`, `[AllowAnonymous]`, in Order.

**Rationale**: the three pages ask the same question with different scopes. Order owns vouchers and needs no other
service for it: the page already knows the shop (shop page, product's `sellerId`) or the cart's shops (the quote's
lines, D4). Anonymous because the shop page and the product page are.

**Alternatives rejected**: Catalog embedding vouchers in the product response - vouchers are Order's (Principle I), and
Catalog would need a copy. A server-side "vouchers for my cart" - it would re-read the cart and re-price it over two
gRPC calls to say what the quote the page already holds can say.

## D3 - What the list says, and what it hides

**Decision**: code, name, platform or shop, benefit, percent, and the request currency's fixed amount, cap and minimum;
the end; the conditions. Never `TotalLimit`, `UsedCount` or `PerCustomerLimit`.

**Rationale**: the shopper needs what it gives and whether it applies; how many are left is the owner's commercial
business, and a count on a public page invites a race to the last use that the claim already settles. "Used up" is
applied as a filter so a voucher nobody can use is not advertised.

## D4 - The checkout's shops come from the quote

**Decision**: `OrderItemResponse` gains `SellerId` (optional, last), filled by the quote and the order reads.

**Rationale**: the quote already carries each line's shop name (specs/036); the id beside it is public (shop pages are
addressed by it) and lets the checkout ask for those shops' vouchers. Optional and last, so an older client ignores it.

## D5 - Order of the list

**Decision**: ending soonest first, open-ended last, then newest; at most 12 (the shop's one page size).

**Rationale**: the vouchers about to end are the ones worth the shopper's attention now; a shop with more than a page of
live vouchers is not a case this shop has.
