# Data Model: Review races and own-product reviews

> Written on 2026-09-27, after the feature merged (#140), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**No table, column, index or migration changed.** The feature changes how existing rows are written.

## `product_reviews` (Catalog, `ecommerce_catalog_db`, from `20260923212320_AddProductReviews`, specs/046)

| Column | Type | Note |
| :--- | :--- | :--- |
| `Id` | `uuid`, PK | |
| `ProductId` | `uuid`, FK → `products` (cascade) | |
| `CustomerId` | `uuid` | |
| `AuthorName` | `character varying(100)` | The token's `given_name` |
| `Rating` | `integer`, check `CK_product_reviews_rating` 1-5 | |
| `Body` | `character varying(2000)`, null | |
| `CreatedAt`, `UpdatedAt` | `timestamp with time zone` | |
| `HiddenAt` | `timestamp with time zone`, null | **The guard column** for hide and restore |
| `HiddenReason` | `character varying(500)`, null | |
| `HiddenBy` | `uuid`, null | |

Indexes: `IX_product_reviews_ProductId_CustomerId` **unique** - the conflict target of the first insert, and the
index whose `23505` reached customers as a 500 before this feature; `IX_product_reviews_ProductId_CreatedAt`.

## The three guarded statements

| Write | Statement | Rows = 1 | Rows = 0 |
| :--- | :--- | :--- | :--- |
| First review | `INSERT ... ON CONFLICT ("ProductId", "CustomerId") DO NOTHING` | stage `ReviewPosted` (+ `NewReview` to the seller), save, recompute | the handler edits the existing review (`ReviewEdited`) |
| Hide | `UPDATE ... SET "HiddenAt", "HiddenReason", "HiddenBy" WHERE "Id" = @id AND "HiddenAt" IS NULL` | stage `ReviewHidden`, save, recompute | 409 "This review is already hidden." |
| Restore | `UPDATE ... SET "HiddenAt" = NULL, "HiddenReason" = NULL, "HiddenBy" = NULL WHERE "Id" = @id AND "HiddenAt" IS NOT NULL` | stage `ReviewRestored`, save, recompute | 409 "This review is not hidden." |

Each runs in one transaction opened inside `CreateExecutionStrategy().ExecuteAsync`.

## State

```text
  (none) ──first write──▶ Visible ──hide (guarded)──▶ Hidden
                             ▲                           │
                             └──── restore (guarded) ────┘
  Visible ──any later write──▶ Visible (edited)
```

## `products` rating (specs/046, unchanged)

`RatingAverage` (`numeric(3,2)`, null) and `RatingCount` (`integer`, default 0) are recomputed from the visible rows
(`count(*)`, `round(avg("Rating"), 2)` where `"HiddenAt" IS NULL`) inside the same transaction as each change -
never incremented.
