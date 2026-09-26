# Research: One rule for "off the shelf"

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #185

---

## D1 - Withdrawn is off the shelf: a 404, not a "no longer sold" page

**Decision**: `OnShelf = IsListed && IsActive`, used by reads as by writes.

**Rationale**:

- Writes, saving and both pricing paths already treated a withdrawn product as off the shelf; only reads disagreed.
  Aligning reads with writes changes the smaller surface and fails closed.
- No command sets `Product.IsActive = false` (D2), so nobody holds a link to a withdrawn product's page today.
- The saved list (specs/075) already shows "no longer available" for anything not on sale, from the list itself.
- A take-down is already a 404 with the seller's and staff's view kept (specs/045, 081); withdrawn behaves the same,
  so there is one kind of "off the shelf" to explain.

**Alternatives considered**:

- **Keep the page, mark it "no longer sold", and let writes stay closed** (the issue's suggestion). Rejected for now:
  it needs a response field and a storefront state for a status no route can produce, and it would make reviews and
  questions of a withdrawn product public while a taken-down one's are not - two kinds of "off the shelf" again.
- **Make writes follow reads (`IsListed` only).** Rejected: would let checkout sell a withdrawn product.

---

## D2 - The survey: who asks, and who writes `IsActive`

**Findings** (all of `server/src/Services/Catalog`):

- Readers of `IsListed`: `ProductReview.MaySee`; `ProductImageKey.MayServe`; `GetProductImageQuery` and
  `GetVariantImageQuery` (the public-cache flag); `ProductViewFeatures` (views); plus `IsListed && IsActive` in
  `ReviewFeatures.OnSale`, `QuestionFeatures`, `SavedProductFeatures` (save, list), `SavedProductNotices`,
  `RecordStockAvailabilityCommandHandler`, `CatalogPricingService` (twice) and `ProductVariant.Sellable`.
- The listing: `ReviewStatus == Approved` in SQL.
- Writers of `Product.IsActive`: none. Only the entity default `true` and the column default.

**Rationale**: the list is the set of call sites changed; the absence of writers sizes the risk (research D1).

---

## D3 - A property on the entity, spelled out once in SQL

**Decision**: `Product.OnShelf` in the Domain, ignored by EF like `IsListed`; the one query that filters in SQL spells
it out with a comment naming the property.

**Rationale**: The rule is about the product alone, so it belongs on the entity where `IsListed` already is. EF cannot
translate a computed property, so the listing writes the two conditions; one comment is cheaper than an expression
helper for one call site.

**Alternatives considered**:

- **A static `ProductReview.OnShelf(product)` in Application.** Rejected: `ProductVariant.Sellable` in the Domain needs
  it too, and the Domain cannot call the Application layer.
- **An `Expression<Func<Product, bool>>` shared by memory and SQL.** Rejected: one SQL caller; more machinery than rule.
- **Rename `IsListed` to mean both.** Rejected: `IsListed` names the review outcome, which moderation code and the
  specs/045 record use in that sense.
