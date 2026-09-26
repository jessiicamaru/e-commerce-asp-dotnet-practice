# Research: Vouchers (part 1 - the server)

> Written on 2026-09-27, after the feature merged (#153), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature**: [spec.md](spec.md) | **Date of the decisions**: 2026-09-26 (decision 52 in
[docs/project/decisions.md](../../docs/project/decisions.md))

The overall design - shop and platform vouchers, one voucher composed of parts, only the server computing - was agreed
with the user on 2026-09-26 (spec). D1-D6 were first recorded in [plan.md](plan.md#research); D7 and D8 are the two
findings the PR records under "Found while building".

> An alternative marked *(reconstructed)* was not recorded when the decision was taken; it is the obvious other
> choice, named in the backfill so the decision reads whole, and its reason for rejection is inferred from the code
> and the recorded rationale.

---

## D1 - Who funds what

**Decision**: A shop voucher comes out of the seller's goods - their part's `GoodsTotal` is `Σ(gross - ShopDiscount)`,
so their commission is taken on less and their payout drops. A platform voucher, and free delivery, are the shop's
cost: they change no seller's terms. A seller cannot create a free-delivery voucher.

**Rationale**: A seller's terms should move only with their own vouchers. Free delivery is platform-only in part 1
because the delivery is split in equal shares between parts (specs/037); a seller paying for it would need a rule for
which share, and the seller's delivery share stays whole.

**Alternatives considered**:

- *(reconstructed)* **The shop funds every voucher.** Rejected: a seller could run a campaign at the shop's expense.
- *(reconstructed)* **Split every voucher proportionally.** Rejected: a platform campaign would silently cut every seller's payout.

---

## D2 - Tax after discount

**Decision**: Tax on a line is on its price after its discounts; tax on delivery is on the delivery after a
free-delivery discount. `Subtotal` and delivery stay the prices before discount; `DiscountTotal` is every voucher's
amount; `Total = Subtotal + Delivery + Tax - Discount`.

**Rationale**: The customer pays tax on what they pay, and a refund of a returned parcel is then exactly what was paid
for it. Keeping `Subtotal` gross keeps the order's parts summing to its total, so the existing CHECK holds.

**Alternatives considered**:

- **Tax before discount.** Rejected: the customer would pay tax on money they never paid; the mutation "tax before the
  discount" is caught by `The_quote_and_the_order_agree…` and `Tax_is_on_the_discounted_price…`.

---

## D3 - A voucher needs an amount row for the currency, even a percentage one

**Decision**: `voucher_amounts` has one row per currency; no row means the voucher is not usable in that currency.

**Rationale**: The cap and the minimum are amounts, and a percentage with no cap in a currency nobody thought about is
an open cheque. Money is never converted (specs/022).

**Alternatives considered**:

- **Percentages usable everywhere, amounts only where set.** Rejected for the reason above.
- *(reconstructed)* **Convert the cap.** Rejected: the shop converts nothing.

---

## D4 - No category targets yet

**Decision**: Targets are `Product` or `Variant` only.

**Rationale**: Order's lines do not know their category. It would need Catalog's pricing answer to carry it, which is a
proto change best made with the screen that needs it.

**Alternatives considered**:

- **Freeze the category on the order line now.** Rejected: a proto change and a Catalog change for no screen yet.

---

## D5 - Two counters, both guarded

**Decision**: `vouchers.UsedCount` and `voucher_customer_uses.Uses`, each claimed by a guarded statement inside the
order's own transaction; the release decrements both, never below 0.

**Rationale**: Counting redemptions instead would race: two concurrent checkouts both count 0. A guarded increment
makes the second of two checkouts for the last use re-read the row after the first commits and move nothing.

**Alternatives considered**:

- **`SELECT count(*) FROM voucher_redemptions`** before inserting. Rejected: the race above.
- *(reconstructed)* **Claim in a separate transaction before the order.** Rejected: a claimed use with no order, or an order whose claim
  failed afterwards.

---

## D6 - An unknown code reads like a disabled one

**Decision**: Both are "Voucher X cannot be used." (409).

**Rationale**: It does not confirm to somebody guessing which codes exist. Every other refusal says why, because the
customer asked for that code and "cannot be used" is otherwise the least useful thing a checkout can say.

**Alternatives considered**:

- *(reconstructed)* **404 for an unknown code.** Rejected: it tells a guesser which codes are real.

---

## D7 - Every hand-opened transaction runs inside the execution strategy

**Decision**: `ClaimAndSaveAsync` and `TryDisableAsync` open their transactions inside
`CreateExecutionStrategy().ExecuteAsync`; the Order test fixture now configures `EnableRetryOnFailure` as production
does.

**Rationale**: Production retries transient failures, and with a retrying strategy a transaction opened by hand outside
a strategy throws. Disabling a voucher answered **500 in the container while all 257 tests passed**, and every
checkout with a voucher would have too; Bruno disabling a voucher is what found it. With the fixture retrying, 12
voucher tests fail without the fix. `NotificationTests.Settling_inside_a_consumer_transaction_joins_it` now opens its
transaction inside the strategy, the way MassTransit does.

**Alternatives considered**:

- *(reconstructed)* **Turn retries off in production.** Rejected: it removes resilience to get a test green.
- *(reconstructed)* **Leave the fixture without retries.** Rejected: it is what let the 500 through.

---

## D8 - Replace the "no discount yet" CHECK rather than keep it

**Decision**: Drop `CK_orders_no_discount_yet` (specs/012 held `DiscountTotal` at 0); add
`CK_orders_discount_not_negative` and `CK_order_items_discounts` (each line's discounts between 0 and its price).

**Rationale**: The first test with a real discount hit the old constraint. The replacement only relaxes the rule, so an
earlier image, which always writes 0, still writes rows it accepts. The schema-compatibility job may comment on the
dropped constraint.

**Alternatives considered**:

- *(reconstructed)* **Keep discounts off the order and apply them to prices.** Rejected: the discount part exists for exactly this, and
  a refund needs to know what was taken off.

---

## Also from the PR

- **Two mutations survived the first round** - the per-customer limit (which pricing checks first) and the release's
  `ReleasedAt IS NULL` (which settlement reaches once anyway). Direct repository tests now catch both:
  `The_claim_refuses_a_use_past_the_customers_limit_whatever_pricing_said` and
  `Releasing_an_order_twice_gives_its_use_back_once`.
- **A racy Bruno check from #148** (`changing my password with a wrong current one is 400`) reused a token revoked in
  the same second; it now signs in afresh first.
