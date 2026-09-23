# Research: A seller can see what they sold

## D1 - The seller is FROZEN onto the line, not looked up live

**Decision**: `order_items.SellerId`, copied from Catalog's pricing answer at submission, never
written again.

specs/031 decided the opposite for stock: Inventory asks Catalog who owns a variant **live, every
time, and never caches it**, because authorization must not be eventually consistent. The two
questions look alike and must be answered in opposite ways, because they are about different things:

| | specs/031 - may this seller stock it? | this - whose sale was it? |
| :-- | :-- | :-- |
| About | a thing **now** | an event **then** |
| Wrong answer costs | a seller refused her own product, or allowed somebody else's | a sale shown to the wrong seller, for good |
| Right source | the owner, at the moment of asking | whatever was true when the customer paid |

An order line is a record of a purchase. It already freezes the price, the name and the options for
exactly this reason - a price change next week must not rewrite what somebody bought - and who sold
it belongs with them. Looking it up live would move a sale to the new owner when a product changes
hands, and would lose it altogether when a product is deleted, which specs/024 allows.

It also keeps both reads inside Order's own database: listing a seller's sales costs no call to
Catalog, and works with Catalog down.

## D2 - The pricing answer carries the seller, with presence

**Decision**: `PricedVariant` gains `optional string seller_id = 9`. Catalog always sets it - to the
seller's id, or to empty for the shop's own product.

The seller has to come from somewhere at checkout, and checkout already asks Catalog one question
about every line (`PriceVariants`). A second call for the seller would be a second round trip on the
critical path, and a second answer that could disagree with the first.

**Why `optional`**: in proto3 a plain string cannot tell "empty" from "not sent". An older Catalog
that knows nothing of the field sends nothing, and that would read as empty - which is exactly what
the shop's own product sends. A sale by a real seller would be written down as the shop's, silently.
With presence, the two are different on the wire: `HasSellerId` false means *this Catalog cannot say*.

**What Order does with "cannot say"**: places the order, records no seller, logs a **warning** naming
the variant (spec FR-003). Refusing a customer's checkout because a reporting field is missing puts
the seller's convenience ahead of the sale itself. The warning is what makes it visible, in Seq,
instead of being discovered as a seller asking why a sale is missing.

It is additive: an older Order ignores the field, and no existing message changes.

## D3 - Only paid orders are sales

**Decision**: `Paid`, legacy `Completed`, `Preparing`, `Shipped`. Not `Submitted`, not `Failed`.

A failed order was never a sale - the payment was declined or the stock was not there. Showing it
would tell a seller they sold something and then take it back. `Submitted` settles in seconds, and
showing it would do the same thing whenever payment then fails.

A `Completed` row is a paid order by its old name (feature 011), so it counts and reads as `Paid`,
through the same `OrderMapping.Describe` everything else uses.

## D4 - A seller sees their lines, and nothing that describes the rest of the order

**Decision**: the response carries the seller's lines and a subtotal over them. It carries **no**
order total, no delivery, no tax total, no customer, no address, no tracking reference.

- **The order's total** includes goods that are not theirs. On a mixed order, reporting it tells
  a seller how much the customer spent elsewhere, and reporting it beside their own lines invites
  reading it as their revenue.
- **Delivery and tax totals** are computed over the whole order. There is no honest per-seller share
  of them until something decides how a delivery charge is split, which is fulfilment's question.
  Each line's own `TaxAmount` is theirs and is shown.
- **The customer and the address**: a seller who only looks has no use for them. When a seller
  ships (the next feature), the address becomes the job, and is disclosed with it - not before.
- **The tracking reference** leads to the address. `Shipped` already says what a seller needs.

The shape of the response is what enforces this - the record has no field for any of them - and a
test asserts it, so adding one is a deliberate act that breaks something.

## D5 - "Not your sale" is a 404, worded like "no such order"

**Decision**: missing, not theirs, failed and still-settling all answer `404 "Sale not found."`

specs/027 for the same reason: a different answer for an order that exists confirms it exists, and
on this endpoint it would confirm that some other seller, or the shop, sold something on that order.
The filter is in the query - `WHERE id = @id AND status IN (...) AND EXISTS (line of mine)` - so there
is no state in which the caller's id has selected an order that is not theirs and then been refused.

## D6 - Seller only, not Seller and Admin

**Decision**: `[Authorize(Roles = "Seller")]` on both.

An administrator already sees every order through fulfilment, with everything on it. "An
administrator's sales" would mean the shop's own - lines with no seller - which is a different query
and one nobody has asked for. Letting an administrator through here would answer with nothing and
look like a bug.

specs/027 learned that opening a write to sellers means changing the attribute too. This is a read,
and new, so there is no inherited attribute to forget - but the Bruno collection proves a customer
gets 403 and an anonymous caller 401, so the attribute cannot quietly go missing.

## D7 - Orders placed before this are nobody's, and stay so

**Decision**: no backfill. Old lines keep a null seller.

Order cannot read Catalog's database, and a migration runs in Order's. A backfill would be a
one-off program asking Catalog about every historical line - and it would answer with **today's**
owner, which D1 exists to refuse. For a practice shop whose orders before today are test debris, it
is not worth writing; the edge case is recorded in the spec rather than hidden.

## D8 - One index

**Decision**: `IX_order_items_SellerId`.

Both reads start from "lines of this seller". Without it that is a scan of every order line in the
shop on every page a seller opens. The column is nullable and most rows will be null for now;
PostgreSQL indexes nulls, which costs a little space and nothing else.
