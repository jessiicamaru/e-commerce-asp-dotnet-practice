# Data Model: Review on every seller edit

> Written on 2026-09-27, after the feature merged (#139), from the code at that merge, the pull request and
> docs/features/catalog.md.

**No table, column, index or migration changed.** Two more writes reach an existing transition.

## `products` - the review columns (specs/045, unchanged)

| Column | Set by `AfterSellerEditAsync` to |
| :--- | :--- |
| `ReviewStatus` (text: `Approved` / `Pending` / `Rejected`, default `Approved`) | `Pending` |
| `ReviewReason` | null |
| `SubmittedAt` | now |

## Transition

```text
Approved ──(seller edits what a shopper reads)──▶ Pending        (off the shelf: Product.IsListed is false)
```

Guarded in code, not SQL: only when `ReviewStatus = Approved`, `SellerId` is not null and the caller is not an
administrator. The status change is staged on the tracked entity and saved with the edit itself.

Edits that reach it at this merge (eight handlers): upload and remove the product image, set and remove a
variant's image, set and remove a product translation, and - since this feature - **set an option translation**
and **add a variant**.

## Rows the two edits write (unchanged tables)

| Edit | Table |
| :--- | :--- |
| Translate an option | `variant_option_translations` - insert or update one row (language, name, value) |
| Add a variant | `product_variants` (with its default-currency `Price`) and its `variant_options`; `ProductVariantCreatedEvent` is published so Inventory can stock it |

## Audit entries (published through Catalog's outbox to Activity)

| Action | Category | Subject | When |
| :--- | :--- | :--- | :--- |
| `OptionTranslated` | Catalog | Product | **New** - every option translation, with before and after |
| `VariantAdded` | Catalog | Variant | Unchanged |
| `ProductSentForReview` | Moderation | Product | When the edit sends an approved seller product back |
