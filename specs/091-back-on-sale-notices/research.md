# Research: A saved product back on sale by any route tells whoever saved it

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #182

---

## D1 - Save, recompute and notify in one transaction

**Decision**: `IProductRepository.SaveAndRecomputeRollupAsync(productId, whenBackInStock)`: inside one transaction
(joining an open one, else inside the execution strategy) - `SaveChangesAsync`, `RecomputeProductRollupAsync`, and
only when that returned true, run the callback and save again. The three edit handlers call it in place of
`SaveChangesAsync(); RecomputeProductRollupAsync();`.

**Rationale**: The three handlers saved, and only then recomputed in a separate auto-committed statement. A notice
staged after that would need a third, separate save - so a crash between them would lose the notice or keep a notice
for a change that rolled back. Principle III requires the message to commit with the change. The flip must be read
after the variant change is saved (the recompute reads the variants from the database), so the save and the recompute
cannot be reordered; wrapping them is the only atomic option.

**Alternatives considered**:

- **Stage the notice before saving, from a flip computed in memory.** Rejected: the rollup is computed in SQL from
  every variant, including ones not loaded; an in-memory guess would disagree with the statement specs/075 made the
  single source of the flip.
- **Publish after the recompute without a transaction.** Rejected: not atomic (above).
- **Recompute inside `SaveChangesAsync` (an interceptor).** Rejected: hides a raw SQL statement and a notification
  behind every save in the service.

---

## D2 - Approval tells, when the product is in stock

**Decision**: In `ProductReviewHandlers.MoveAsync`'s `stage` callback - which runs inside `TryReviewAsync`'s
transaction only for the call whose guarded move won - when `to == Approved` and the product is active and available,
call `SavedProductNotices.BackOnSaleAsync`.

**Rationale**: Approval is the only route that lists a product (`Pending` → `Approved`), and the guarded move makes
it happen once. A saver can only have saved a listed product, so an approval with savers is a return. The stock check
avoids telling somebody about a product they still cannot buy; Inventory's route tells them when units arrive, and its
own listed check (unchanged) now passes because the product is approved.

**Alternatives considered**:

- **Tell on every approval regardless of stock.** Rejected: "available again" would be false; held by
  `Approving_a_product_with_nothing_in_stock_tells_nobody`.
- **Recompute the rollup on approval and use its flip.** Rejected: approval does not change availability; the flip
  that matters is "listed", which the guarded move already decides.

---

## D3 - The routes, surveyed

**Decision**: Record what was found; it bounded the fix.

**Findings**:

- `RecomputeProductRollupAsync` callers: `RecordStockAvailability` (used the flip), `SetVariantPrice`,
  `AddProductVariant`, `UpdateProductVariant` (discarded it).
- Writers of `ReviewStatus` to `Approved`: `ApproveProductCommand` only (`Pending` → `Approved`). Take-down goes to
  `Rejected`; a rejected product returns through `ResubmitProductCommand` → `Pending` → approval.
- Writers of `Product.IsActive`: **none** - only the entity's default `true`. So "a withdrawn product reactivated" is
  not a route today.
- `RemoveVariantPriceCommand` does not recompute the rollup.

---

## D4 - A price alone is not "back on sale"

**Decision**: No notice for setting a price, beyond the uniform "the recompute flipped" rule.

**Rationale**: The rollup's `Availability` is `BOOL_OR` over active variants' availability and ignores price. A price
in a new currency makes the variant sellable **to shoppers paying in that currency** (specs/022); Catalog does not know
which currency a saver uses, so it cannot say "you can buy it now" truthfully to a particular person.

**Alternatives considered**:

- **A notice when the first price in any currency is set.** Rejected: most savers pay in dong and would be told about
  a dollar price they cannot use.

---

## D5 - One kind, reworded; one helper

**Decision**: Keep `SavedBackInStock` for every route; change its default words to "available again" (en) /
"đã có thể mua lại" (vi) in the notice, the admin label and the email; send through one helper,
`SavedProductNotices.BackOnSaleAsync`.

**Rationale**: The message is the same to the reader - "the thing you saved can be bought again". A new kind would need
`notification-kinds.json`, two locale strings, an `EmailTemplate` constant, words in two languages, an admin label and
the tests that hold them together (specs/048, 083), for no difference the reader sees. Renaming the kind would leave
stored notices with a kind the storefront no longer knows (they would read "a new update"). One helper means the kind,
data keys, link and template are written once.

**Alternatives considered**:

- **A new kind `SavedBackOnSale`.** Rejected for the cost above.
- **Keep "back in stock".** Rejected: false after an approval of a product that never left stock.
