# Data Model: No reviews off the shelf

> Written on 2026-09-27, after the feature merged (#177), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration changed.** The feature decides, before a write, whether the write may happen.

## What is read

| Table | Columns | Why |
| :--- | :--- | :--- |
| `products` | `ReviewStatus` (text: `Approved` / `Pending` / `Rejected`), `IsActive`, `SellerId` | On sale is `ReviewStatus = 'Approved' AND IsActive` |
| `review_eligibility` | `(CustomerId, ProductId)` | Whether the customer received it (specs/046), unchanged |

## What is protected

| Table | Columns | Before | After |
| :--- | :--- | :--- | :--- |
| `product_reviews` | the customer's row | could be inserted or changed off the shelf | not written off the shelf |
| `products` | `RatingAverage`, `RatingCount` | recomputed after such a write | untouched off the shelf |

## Product states and the write

| `ReviewStatus` | `IsActive` | Write a review | `reviews/mine` `eligible` |
| :--- | :--- | :--- | :--- |
| `Approved` | true | as before (403 if own product or not received) | as before |
| `Approved` | false | 404 `Product not found.` | false |
| `Pending` / `Rejected` (incl. taken down) | any | 404 `Product not found.` | false |
| no such product | - | 404 `Product not found.` | false |

## Schema evolution

Nothing to evolve. An earlier Catalog image accepts the write again.
