# Research: A seller can stock what they sell

> Completed on 2026-09-27, after the feature merged (#71), from the code at that merge, the pull request and
> docs/features/marketplace.md. The decisions and their prose are as written before the code; the
> **Decision / Rationale / Alternatives considered** labels were added so each reads like specs/001.

**Feature**: [spec.md](spec.md)

## D1 - Where does the ownership check happen?

**Decision**: Inventory asks Catalog over gRPC, once per stock write, and decides for itself.

**Rationale**: Catalog already serves gRPC on a second port and already answers questions about variants
(`PriceVariants`, `DescribeVariants`). This is one more question to the service that owns the
answer, on an action a person performs by hand a few times a day — not on a hot path, not on
checkout, not per order line.

The cost is real and accepted: **Catalog unreachable means a seller cannot stock.** That is
tolerable because Catalog unreachable also means nobody can see the product, so nothing is lost by
also being unable to stock it. It is the same trade specs/009 made for pricing, on a far less
frequent operation.

As built, the client is bounded like Order's: three attempts, a five-second deadline each, backoff of
200 ms then 1 s, and on exhaustion a **503** (`DependencyUnavailableException`) — never a 404, because
"I could not find out whether this is yours" is not "this is not yours".

**Alternatives considered**: a read model in Inventory (D2) and Catalog proxying the write (D3), both
rejected below.

## D2 - Why NOT a read model of ownership in Inventory?

**Decision**: no copy of ownership in Inventory; the answer is asked live and never cached.

**Rationale**: This is the option the codebase's own habits point at: Catalog keeps a `sellers` read model
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

**Alternatives considered**: the read model itself, rejected for the reasons above; a short-lived cache
of the live answer, rejected for the same reason at a smaller scale — it is still a copy that can be
behind.

## D3 - Why not have Catalog proxy the write?

`PUT /api/catalog/products/{id}/variants/{vid}/stock`, Catalog checks ownership it already knows,
and relays to Inventory.

**Decision**: rejected. Inventory stays the only service that writes stock.

**Rationale**: It keeps the rule where the data is, which is genuinely attractive, but it puts a
write-through for somebody else's data in Catalog — and then two services can write stock, one of
them without the `FOR UPDATE` lock and the reserved-quantity check that make the write safe. The
rule is easy to move; the invariant is not. Inventory owns stock (specs/004) and that is the line
worth holding.

It also means the storefront's stock control talks to a different service from the stock it reads,
which is the kind of asymmetry nobody remembers a year later.

**Alternatives considered**: a `MayStock(variant, caller)` RPC in Catalog that decides rather than
answers — rejected in `CatalogOwnershipService`'s own remarks: it would move an authorization rule into
the service that does not hold the row being written, and would have to be told who is asking, the
shape of the defect specs/009 and specs/027 fixed.

## D4 - 404, never 403

**Decision**: somebody else's variant is 404, worded exactly as a variant that does not exist:
`Product with ID '…' was not found.` (`StockOwnership.NotFound`).

**Rationale**: Unchanged from specs/027 and stated again because this is a new service learning the rule: a 403 on
somebody else's variant confirms the id is real and belongs to someone, which turns a stock endpoint
into an enumeration tool for the catalogue's private structure.

Worth noting what this means for an **administrator**: they pass everything, as they do in Catalog,
because moderating a marketplace is the job. As built, an administrator does not reach Catalog at
all — asking would spend a call to learn nothing and would make administering stock fail exactly
when Catalog is down (`An_administrator_does_not_cost_a_call_to_Catalog`).

**Alternatives considered**: 403 for another seller's variant — rejected, it confirms the id.

## D5 - Absolute quantity, and the lost update that is already handled

The issue asked whether `PUT` should become a delta, because "set it to 5" while an order is
reserving looks like a lost update.

**Decision**: stays absolute. Nothing changes.

**Rationale**: **It is already prevented, and I was wrong to raise it as open.** `SetStockOnHandCommandHandler`
takes the same `FOR UPDATE` lock as the reserve path — its comment says so — and refuses any value
below `QuantityReserved` with a `ConflictException`. The command's own summary reads *"Absolute
value, never a delta: two admins adjusting at once would otherwise lose one update."*

So the decision was made, documented and guarded before this feature existed. Nothing here changes
it, and the tests that cover it must keep passing.

**Alternatives considered**: a delta endpoint — rejected in specs/001's HTTP contract already ("a delta
endpoint invites lost updates from concurrent callers").

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

**Rationale**: The ownership check runs **first**: if Catalog says the product exists and is hers, a missing stock
row is a timing answer she can retry, worded as such. If Catalog says it is not hers — or does not
exist — it is the flat 404 from D4 with nothing else said.

That ordering is the whole of it, and it is only correct because D1 asks Catalog live: a read model
could not distinguish these two cases at all.

**Alternatives considered**: one 404 for all three cases — rejected above. No other alternative is
recorded.

## D7 - May a seller see what is reserved?

**Decision**: yes, for their own products.

**Rationale**: `quantityReserved` is how many of her units are inside somebody's checkout right now. Without it,
"I have 3 on hand but only 1 available" looks like a bug rather than like two sales in progress.
`GET /api/stock/{id}` is already anonymous and already returns it, so this needs no change — it is
recorded because the seller console will show it and somebody will ask whether that was decided.

**Alternatives considered**: showing available only — rejected for the reason above.

## D8 - Does the create form get a quantity field?

**Decision**: no. Stocking is a second step, on the listing's own page.

**Rationale**: A field on the create form would have to write stock immediately after creating the product, which
is precisely the window in D6 — the row it needs does not exist yet. Making the form retry a broker
round-trip to fill in a number is a lot of machinery to save one click, and it would fail in a way
the seller reads as "the product was not created".

**Alternatives considered**: the field with a retrying write — rejected above.

## D9 - A new proto, not a method on `CatalogPricing`

**Decision**: `CatalogOwnership` is its own service in its own `catalog_ownership.proto`, with a plural
request.

**Rationale**: "who owns this" is an input to an **authorization** decision, and burying it in a
service called Pricing means the next person looking for the rule does not find it. Plural because
`PriceVariants` learned that a per-item call leaves the caller holding partial state; a missing
variant is **absent** from the response rather than an error, so the caller's own 404 stays worded
identically for "missing" and "not yours". (Recorded in [contracts/api.md](contracts/api.md) at the
time; restated here as a decision in this backfill.)

**Alternatives considered**: `GetVariantOwner` on `CatalogPricing` — rejected above; a singular RPC —
rejected above.
