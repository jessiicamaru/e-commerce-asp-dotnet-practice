# Quickstart: Validating review races and own-product reviews

> Written on 2026-09-27, after the feature merged (#140), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d        # Catalog's database on 5433
```

## Scenario 1 - The three tests (US1, US2, US3, SC-001 to SC-003)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ReviewTests"
```

**Expected**: green, including

| Test | Proves | Failed before the fix with |
| :-- | :-- | :-- |
| `First_reviews_posted_at_once_are_one_review_posted_once` | six concurrent first writes → one review, one `ReviewPosted`, one `NewReview`, count 1 | `23505 duplicate key ... IX_product_reviews_ProductId_CustomerId` |
| `A_review_is_hidden_once_and_restored_once_however_many_moderators_press_at_once` | five hides → 1 success, 4 × 409, one `ReviewHidden`, rating gone; five restores → the same, rating back | several of five hides succeeding |
| `A_seller_cannot_review_their_own_product` | 403 "your own product"; `GetMyReviewQuery` not eligible | no refusal |

The whole project: 160 tests at the merge.

## Scenario 2 - Repeat the races (SC-004)

```bash
for i in 1 2 3 4 5; do
  DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --no-build \
    --filter "FullyQualifiedName~First_reviews_posted_at_once|FullyQualifiedName~hidden_once_and_restored_once"
done
```

**Expected**: 5/5 green, as the pull request recorded.

## Scenario 3 - Mutation checks

The pull request's, each restored:

| Mutation | Expected |
| :-- | :-- |
| Insert without `ON CONFLICT` | 2 red |
| Hide without its guard (the `HiddenAt == null` condition removed) | 2 red |
| No own-product rule | 1 red |

## Scenario 4 - In the database (by hand)

```sql
-- ecommerce_catalog_db on 5433: never more than one review per customer per product
SELECT "ProductId", "CustomerId", count(*) FROM product_reviews GROUP BY 1, 2 HAVING count(*) > 1;   -- no rows
```

The unique index guarantees this; the query is a sanity check, not recorded as run for this feature.
