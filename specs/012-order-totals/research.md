# Research: A Total With Something Behind It

> Completed on 2026-09-27, after the feature merged (#32), from the code at that merge, the pull
> request and docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

Six decisions, each taken on the owner's instruction to choose the recommended option rather than
ask (spec, "Decisions already taken"). Each records what was chosen, why, and what was rejected.

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

As built: `ConfiguredTaxRates` reads the section once, in its constructor, upper-cases the country
keys, and throws `InvalidOperationException` naming the offending entry for a missing default, a
value that is not a number, or a rate outside `[0, 1)`. `Program.cs` resolves `ITaxRates` right after
`builder.Build()`, so a bad rate stops the service at startup with the reason in the log instead of
turning every checkout into a 500. The shipped defaults are VN 0.10, GB 0.20, DE 0.19, US 0.00 and a
default of 0.10.

## D3 — Rounding

**Decision**: `lineTax = round(unitPrice × qty × rate, 2, AwayFromZero)`, the same for delivery,
`tax = Σ lineTax + deliveryTax`. `MidpointRounding.AwayFromZero` — .NET's default is banker's rounding
(`ToEven`), which surprises anyone reading a receipt.

**Consequence, accepted**: per-line rounding can differ from rounding the sum by up to half a cent per
line. It is not "corrected" afterwards — the stored per-line tax is what was charged.

**Rationale**: a customer checking a receipt by hand rounds half up; a rule that sometimes rounds 0.025
down to 0.02 reads as an error even when it is documented. Per line, because each line's tax is stored
and shown, and a line's tax should not depend on what else is in the basket. The pull request measured
the difference on purpose: three lines of 0.25 at 10% give 0.09 of tax per line, not the 0.08 that
rounding the sum would give.

**Alternatives considered**:

- **Banker's rounding (`ToEven`)** - .NET's default. Rejected for the surprise above; the negative
  control in tasks T011 shows that swapping it in fails 2 of the 6 arithmetic tests.
- **Tax on the order's sum, rounded once.** Rejected: the per-line figure would then have to be an
  allocation of the sum, with a remainder rule, and the stored line tax would no longer be "the tax on
  that line".

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

**As built - the two shorter constraints above are written more exactly in the migration.** Both carry
the same `IS NULL` escape as the sum, for the same reason, and the rate range is half-open:

```sql
CK_orders_no_discount_yet   "DiscountTotal" IS NULL OR "DiscountTotal" = 0
CK_orders_tax_rate_range    "TaxRate" IS NULL OR ("TaxRate" >= 0 AND "TaxRate" < 1)
```

"Between 0 and 1" above means `0 ≤ rate < 1`, matching D2's validation; a rate of exactly 1 is refused.
The backfill runs **before** the constraints are added, so they are validated against rows that already
satisfy them (see [data-model.md](./data-model.md)).

**Alternatives considered**:

- **NOT NULL columns with defaults.** Rejected: a default of 0 for `Subtotal` would make the sum
  constraint refuse every order an older image inserts, and a rollback would stop checkout.
- **The sum enforced in code only.** Rejected: the constitution's persistence rule wants an invariant
  that matters as a constraint, and "the parts add up to what was charged" is the one this feature is
  about.

## D5 — No contract change

`OrderSubmittedEvent.TotalAmount` is the grand total, and the saga passes it to Payment unchanged. The
same number, a different sum.

**Decision**: leave every message in `Ecommerce.Contracts` as it is.

**Rationale**: the saga and Payment only need the amount to charge, and that is still one number. A
contract change would have meant rebuilding the Orchestrator and Payment for no behavioural gain - and
this repository has already learned that a relaying service left on an older contract drops fields in
silence (CLAUDE.md, gotchas).

**Alternatives considered**: carrying the parts on `OrderSubmittedEvent`. Rejected: nobody downstream
reads them, and a contract with fields nobody consumes states that a service says something it does
not act on.

## D6 — Proof

A pure `OrderTotals.Compute` function carries the arithmetic and is unit-tested (half-cent case,
zero rate, several lines). A database test inserts a row whose parts do not sum and expects a
constraint violation. `verify-saga.sh` recomputes the tax from the order's stored rate with the same
rule and asserts the payment amount equals the stored total.

**Decision**: three layers - pure arithmetic tests, a real-database constraint test, and an end-to-end
recomputation that does not trust Order's own figure.

**Rationale**: constitution principle V. A test that reads Order's tax and checks the parts sum would
pass against wrong arithmetic that sums consistently; the script recomputes the tax in Python `Decimal`
with `ROUND_HALF_UP` from the rate the order stored, so a wrong rule in Order shows up as a mismatch.
The constraint test runs against PostgreSQL because the guarantee under test belongs to the database.

**Alternatives considered**: asserting only in the unit tests. Rejected: they prove the function, not
that the handler stored what it returned or that Payment was asked for it.
