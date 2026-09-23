# Research: A seller can stock what they sell

## D1 - Where does the ownership check happen?

**Decision**: Inventory asks Catalog over gRPC, once per stock write, and decides for itself.

Catalog already serves gRPC on a second port and already answers questions about variants
(`PriceVariants`, `DescribeVariants`). This is one more question to the service that owns the
answer, on an action a person performs by hand a few times a day — not on a hot path, not on
checkout, not per order line.

The cost is real and accepted: **Catalog unreachable means a seller cannot stock.** That is
tolerable because Catalog unreachable also means nobody can see the product, so nothing is lost by
also being unable to stock it. It is the same trade specs/009 made for pricing, on a far less
frequent operation.

## D2 - Why NOT a read model of ownership in Inventory?

This is the option the codebase's own habits point at: Catalog keeps a `sellers` read model
(specs/027), Catalog keeps an `Availability` read model (specs/004), and both are fed by events.
Doing it a third time would look consistent.

**Rejected, and the reason is the whole of this decision: authorization must not be eventually
consistent.**

A read model is seconds behind by design. A seller who lists a product and immediately tries to
stock it would be told **404** — which is, deliberately, *the same answer as "this is not yours"*
(specs/027). She would have no way to tell "wait a moment" from "you do not own this", and neither
would anybody reading the logs. The indistinguishability that makes 404 the right answer for a
security question is exactly what makes it the wrong answer for a timing one.

A read model would also need a backfill for every existing product, and a new event whose only
consumer is an authorization rule — a rule that is wrong in the window and silent about it.

**The read models this project already has are all about display**: a shop name under a listing, an
in-stock badge. Being a second out of date costs a slightly stale page. Being a second out of date
on "may you write this" costs a refusal that looks like an accusation.

## D3 - Why not have Catalog proxy the write?

`PUT /api/catalog/products/{id}/variants/{vid}/stock`, Catalog checks ownership it already knows,
and relays to Inventory.

**Rejected.** It keeps the rule where the data is, which is genuinely attractive, but it puts a
write-through for somebody else's data in Catalog — and then two services can write stock, one of
them without the `FOR UPDATE` lock and the reserved-quantity check that make the write safe. The
rule is easy to move; the invariant is not. Inventory owns stock (specs/004) and that is the line
worth holding.

It also means the storefront's stock control talks to a different service from the stock it reads,
which is the kind of asymmetry nobody remembers a year later.

## D4 - 404, never 403

Unchanged from specs/027 and stated again because this is a new service learning the rule: a 403 on
somebody else's variant confirms the id is real and belongs to someone, which turns a stock endpoint
into an enumeration tool for the catalogue's private structure.

Worth noting what this means for an **administrator**: they pass everything, as they do in Catalog,
because moderating a marketplace is the job.

## D5 - Absolute quantity, and the lost update that is already handled

The issue asked whether `PUT` should become a delta, because "set it to 5" while an order is
reserving looks like a lost update.

**It is already prevented, and I was wrong to raise it as open.** `SetStockOnHandCommandHandler`
takes the same `FOR UPDATE` lock as the reserve path — its comment says so — and refuses any value
below `QuantityReserved` with a `ConflictException`. The command's own summary reads *"Absolute
value, never a delta: two admins adjusting at once would otherwise lose one update."*

So the decision was made, documented and guarded before this feature existed. Nothing here changes
it, and the tests that cover it must keep passing.

## D6 - The window where the stock row does not exist yet

Measured while reproducing #70:

```
POST /api/products            -> 200
GET  /api/stock/{variantId}   -> 404  "Product '…' is not registered in inventory."
   ... a few seconds later ...
GET  /api/stock/{variantId}   -> 200  {"quantityOnHand":0, …}
```

`ProductCreatedConsumer` creates the row off the broker, so it arrives asynchronously.

**Decision**: the two 404s must be told apart. "This variant is not yours" and "this variant's stock
row has not been created yet" are the same status code and must not be the same *message*, or a
seller retrying forever is indistinguishable from a seller being refused.

The ownership check runs **first**: if Catalog says the product exists and is hers, a missing stock
row is a timing answer she can retry, worded as such. If Catalog says it is not hers — or does not
exist — it is the flat 404 from D4 with nothing else said.

That ordering is the whole of it, and it is only correct because D1 asks Catalog live: a read model
could not distinguish these two cases at all.

## D7 - May a seller see what is reserved?

**Decision**: yes, for their own products.

`quantityReserved` is how many of her units are inside somebody's checkout right now. Without it,
"I have 3 on hand but only 1 available" looks like a bug rather than like two sales in progress.
`GET /api/stock/{id}` is already anonymous and already returns it, so this needs no change — it is
recorded because the seller console will show it and somebody will ask whether that was decided.

## D8 - Does the create form get a quantity field?

**Decision**: no. Stocking is a second step, on the listing's own page.

A field on the create form would have to write stock immediately after creating the product, which
is precisely the window in D6 — the row it needs does not exist yet. Making the form retry a broker
round-trip to fill in a number is a lot of machinery to save one click, and it would fail in a way
the seller reads as "the product was not created".
