# HTTP Contract: No reviews off the shelf

> Written on 2026-09-27, after the feature merged (#177), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](../spec.md)

Two existing Catalog endpoints, through the gateway on `:5000` (`catalog-products-route`). No shape changed; the
answers for a product off the shelf did. No message or gRPC contract changed.

---

## `PUT /api/products/{id}/reviews/mine` - `Customer`

Body (unchanged):

```json
{ "rating": 4, "body": "Fine again." }
```

| Situation | Before | After |
| :--- | :--- | :--- |
| On sale, received, not own | `200` `ReviewResponse` | `200` (unchanged) |
| On sale, own product | `403` `You cannot review your own product.` | `403` (unchanged) |
| On sale, not received | `403` `Only a customer who has received this product can review it.` | `403` (unchanged) |
| **Off the shelf** (pending, rejected, taken down) or inactive, received | `200`, and the rating moved | **`404` `Product not found.`** |
| **Off the shelf** or inactive, not received | `403` (confirmed the product exists) | **`404` `Product not found.`** |
| No such product | `404` `Product with ID '<id>' was not found.` | `404` `Product not found.` |

## `GET /api/products/{id}/reviews/mine` - signed in

Response (unchanged shape):

```json
{ "eligible": false, "review": { "id": "...", "rating": 5, "body": "Sharp and light.", "...": "..." } }
```

`eligible` is now false for a product off the shelf or inactive (and, as before, for the product's own seller and
for a customer who did not receive it). `review` is still the caller's existing review, or null.

## Authorization

Unchanged: `[Authorize(Roles = "Customer")]` on the write, `[Authorize]` on the read. The reviewer is the token's
user.
