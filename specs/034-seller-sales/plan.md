# Implementation Plan: A seller can see what they sold

**Branch**: `034-seller-sales` | **Spec**: [spec.md](spec.md) | **Closes**: #75

## Technical Context

- **Catalog**: `PricedVariant` gains `optional string seller_id` (research D2); `CatalogPricingService.Describe` fills it.
- **Order**: `OrderItem.SellerId` + migration + index; `CatalogPrice` and `PricedLine` carry it;
  `SubmitOrderCommandHandler` freezes it; `GrpcCatalogPrices` turns an absent field into null plus a
  warning; two queries and two `Seller`-only routes.
- **Client**: `Order.sales` / `Order.sale`, two hooks, two pages under `/shop/sales`, a link from the
  shop page, both languages, Vitest tests.
- **Bruno**: the list, a refused sale, a customer's 403, an anonymous 401.

No new message, no new service edge: checkout already asks Catalog this exact question.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Both reads are inside Order's own database. The seller arrives on an answer Order already asks for; no new synchronous edge. |
| II - Clean Architecture | The proto field is read in Infrastructure (`GrpcCatalogPrices`); Application sees `Guid?` and never learns about presence on a wire. |
| III - Atomic writes, idempotent messaging | The column is written in the same `SaveChanges` as the rest of the order and its outbox message. Nothing new is published. |
| IV - Identity from the token | The seller is `ICurrentUser.Id`. Neither route accepts a seller id. Refusal is 404, worded like "no such order". |
| V - Evidence over assumption | Every refusal has a test that fails without its guard: another seller's line, a failed order, a still-settling order, an absent seller field. The response's shape is asserted, not assumed. |

No Complexity Tracking entries.

## The decision most likely to be copied wrongly

⚠️ **Frozen here, live in specs/031** (research D1). A future reader who sees Inventory asking
Catalog live will be tempted to "make it consistent". The difference is that one is a permission on
a thing now and the other is a record of an event then; the CLAUDE.md paragraph says so beside
specs/031's.

## Phases

1. **Contract** - the proto field; Catalog fills it; a Catalog test that a seller's variant carries
   the seller and the shop's carries empty-but-present.
2. **Freeze** - column, migration, index; `CatalogPrice.SellerId`; `PricedLine.SellerId`; the handler
   copies it. Tests: a seller's line, the shop's line, a mixed order.
3. **Absent means unknown** - `GrpcCatalogPrices` maps `HasSellerId == false` to null and warns.
4. **Reads** - repository methods, two queries, validator, controller routes. Tests seen red first:
   another seller's line, failed, submitted, legacy `Completed`, the subtotal, the response shape,
   paging.
5. **Storefront** - service, hooks, pages, routes, locales, link; Vitest tests.
6. **Bruno** - four requests.
7. **End to end** - on the running stack: a seller lists, prices and stocks a product; a customer
   buys it and it pays; the seller sees it; a second seller does not; `verify-saga.sh`.
8. **Say so** - CLAUDE.md, test counts.

## Verification

- Order 61 → about 72; Catalog 130 → 132; client 43 → about 50.
- Bruno green headless, with the credentials exported on their own line (CLAUDE.md warning).
- `verify-saga.sh` passes: checkout is the path this changes.
