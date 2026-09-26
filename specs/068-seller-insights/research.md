# Research: A seller sees how their shop is doing

> Written on 2026-09-27, after the feature merged (#152), from the code at that merge, the pull request and
> docs/features/seller-insights.md.

**Feature**: [spec.md](spec.md) | **Date of the decisions**: 2026-09-25 (decision 51 in
[docs/project/decisions.md](../../docs/project/decisions.md))

D1 to D4 were first recorded in [plan.md](plan.md#research); they are set out here with their alternatives, and D5 is
added from the code. Who took each decision is not recorded.

> An alternative marked *(reconstructed)* was not recorded when the decision was taken; it is the obvious other
> choice, named in the backfill so the decision reads whole, and its reason for rejection is inferred from the code
> and the recorded rationale.

---

## D1 - A received return is not revenue on the seller's page

**Decision**: A seller's lines in a part (`order_shipments` of `(OrderId, SellerId)`) whose return reached `Received`
are left out. A return still open counts - it may yet be refused. The rule is per seller's part: another seller's line
on the same order still counts for them.

**Rationale**: The number sits beside the seller's earnings, which already exclude a returned part (specs/066). Two
answers to "what did I make" on one console would be read as a bug.

**Alternatives considered**:

- **Count it, as the admin Overview did at the time.** Rejected for the seller for the reason above. Fixing the
  Overview belonged in its own change (it came in specs/084).
- **Exclude any part with a return, open or not.** Rejected: an open return may be refused, and the money would
  reappear.

---

## D2 - The same response shapes as the admin's

**Decision**: `GetSellerRevenueQuery` answers `RevenueResponse` and `GetSellerTopProductsQuery` answers
`List<TopProduct>`; the handler's grouping (rows in, totals and days out) is shared with the admin's queries.

**Rationale**: One chart and one set of types, rather than a parallel "seller" family that drifts. The admin's
`InsightsTests` still pass on the shared grouping.

**Alternatives considered**:

- **Seller-specific records.** Rejected: two families of near-identical types and two charts.

---

## D3 - The rating comes from each product's stored average

**Decision**: The shop's rating is `Σ(RatingAverage × RatingCount) / Σ RatingCount` over the seller's products with at
least one review, rounded to 2 places away from zero; null when there is none.

**Rationale**: `products.RatingAverage` and `RatingCount` are recomputed from the visible reviews in the transaction of
every review write, hide and restore (specs/046). Counting reviews again would be a second definition of the same
number. Weighting by count is what makes it the average over every review: one review at 4 and three at 2 give 2.5,
not 3.0.

**Alternatives considered**:

- **Average of the product averages.** Rejected: a product with one review would weigh as much as one with a hundred.
  The mutation "average of averages" is caught by `The_shops_rating_is_weighted_by_each_products_review_count`.
- **Re-aggregate `product_reviews`.** Rejected: a second definition of "visible review".

---

## D4 - No seller id in any request

**Decision**: All three endpoints read the seller from `ICurrentUser`; none takes a seller id.

**Rationale**: Constitution IV, and the same shape as `/api/orders/sales`. A caller whose token has no id is refused,
never read as "the shop's own" (`SellerId == null` means the shop in this codebase).

**Alternatives considered**:

- *(reconstructed)* **`?sellerId=` with an ownership check.** Rejected: an id the caller can change is the defect the principle exists
  to prevent.

---

## D5 - Grouping in SQL, over a class projection

**Decision**: `SellerLinesIn` projects to a private class with an object initializer (`SellerLine`), then groups by day
and currency (revenue, distinct orders) or by product and currency (units, revenue, newest name).

**Rationale**: One round trip per figure. EF cannot group over a projection built with a positional record's
constructor (found in specs/066), hence the class. Orders are counted distinct per day and currency because one order
can hold several of a seller's lines.

**Alternatives considered**:

- *(reconstructed)* **Load the lines and group in memory.** Rejected: it grows with the seller's history.
