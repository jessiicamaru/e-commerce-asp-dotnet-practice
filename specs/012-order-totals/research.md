# Research: A Total With Something Behind It

## D1 — Prices exclude tax (ADR-002)

**Decision**: Catalogue prices are net. Tax is added at checkout and shown as its own figure.

**Rationale**: Every price already in the catalogue keeps its meaning — nothing is reinterpreted.
"Tax varies with destination" is then visible in the total, and the parts sum with no "of which"
line (subtotal + delivery + tax − discount = total).

**Rejected**: tax-inclusive prices, the norm for consumer shops in the EU and Vietnam. Every existing
price would silently become gross, the tax would have to be *extracted* per destination (so the net
a merchant keeps changes with the destination), and the total's parts stop summing — tax becomes a
component *of* the subtotal. Worth revisiting if this shop ever sells to consumers under a regime
that requires gross display; the change then is display plus extraction, recorded in the ADR.

## D2 — Order computes tax, from configuration

**Decision**: `Tax:DefaultRate` and `Tax:Rates` (`{ "VN": 0.10, "GB": 0.20, ... }`) in Order's
configuration, validated at startup (a default present; every rate `0 ≤ r < 1`).

**Rationale**: Order already decides what is charged (feature 009 prices, 011 delivery). A tax
service would own one table and add a fourth synchronous dependency to checkout.

**Rejected**: Catalog (prices products, not destinations); a Tax service (above).

## D3 — Rounding

**Decision**: `lineTax = round(unitPrice × qty × rate, 2, AwayFromZero)`, the same for delivery,
`tax = Σ lineTax + deliveryTax`. `MidpointRounding.AwayFromZero` — .NET's default is banker's rounding
(`ToEven`), which surprises anyone reading a receipt.

**Consequence, accepted**: per-line rounding can differ from rounding the sum by up to half a cent per
line. It is not "corrected" afterwards — the stored per-line tax is what was charged.

## D4 — Storage and the sum constraint

**Decision**: new nullable columns on `orders` — `Subtotal`, `TaxTotal`, `DiscountTotal`, `TaxRate`
(`numeric(5,4)`) — and `TaxAmount` on `order_items`. `ShippingPrice` (feature 011) is the delivery
part. A CHECK constraint:

```sql
"Subtotal" IS NULL
OR "Subtotal" + COALESCE("ShippingPrice", 0) + "TaxTotal" - "DiscountTotal" = "TotalAmount"
```

plus `CHECK ("DiscountTotal" = 0)` while discounts are out of scope, and `TaxRate` between 0 and 1.

**Why nullable, and why the `IS NULL` escape**: the constitution requires a schema the previous image
can still run against. An image from before this feature inserts orders without these columns; a
NOT NULL column (or a CHECK that bites on NULL) would make its checkouts fail. Existing rows are
backfilled (`Subtotal = TotalAmount − COALESCE(ShippingPrice, 0)`, tax 0, discount 0, rate 0) so every
row the new image reads has its parts.

## D5 — No contract change

`OrderSubmittedEvent.TotalAmount` is the grand total, and the saga passes it to Payment unchanged. The
same number, a different sum.

## D6 — Proof

A pure `OrderTotals.Compute` function carries the arithmetic and is unit-tested (half-cent case,
zero rate, several lines). A database test inserts a row whose parts do not sum and expects a
constraint violation. `verify-saga.sh` recomputes the tax from the order's stored rate with the same
rule and asserts the payment amount equals the stored total.
