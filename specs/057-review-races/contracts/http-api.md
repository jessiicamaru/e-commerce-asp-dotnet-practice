# HTTP Contract: Review races and own-product reviews

> Written on 2026-09-27, after the feature merged (#140), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](../spec.md)

No route, request or response body changed. Four existing endpoints answer differently in the cases below. No
message contract changed (`AuditEntryRecorded` and `UserNotificationRequested` are published as before, now only
for a write that happened); no gRPC contract changed. All through the gateway on :5000.

## `PUT /api/products/{productId}/reviews/mine` - `Customer`

**Request** (unchanged): `{ "rating": 5, "body": "..." }` (rating 1-5, body at most 2000).

| Status | When | Change |
| :--- | :--- | :--- |
| `200` | Written - first review or an edit | **Now also** when several first writes race: the one that inserts is the review, each other one edits it. Before, the loser got `500` |
| `403` | **New**: the caller is the product's seller | `detail` "You cannot review your own product." - checked before eligibility |
| `403` | Not received | `detail` "Only a customer who has received this product can review it." (unchanged) |
| `404` | No such product | unchanged |

Only the write that inserts records `ReviewPosted` and tells the seller (`NewReview`); a losing write records
`ReviewEdited`.

## `GET /api/products/{productId}/reviews/mine` - signed in

**Response** (shape unchanged): `{ "eligible": false, "review": null }`. `eligible` is now **false for the product's
own seller**, whether or not they received it, so the page does not offer the form.

## `POST /api/reviews/{id}/hide` - `Admin` or `Moderator`

**Request** (unchanged): `{ "reason": "Advertising" }`.

| Status | When |
| :--- | :--- |
| `200` | This call hid it (one guarded `UPDATE`); `ReviewHidden` recorded, rating recomputed, in one transaction |
| `409` | Already hidden - **including by a concurrent call**; nothing recorded. `detail` "This review is already hidden." |
| `404` | No such review |

## `POST /api/reviews/{id}/restore` - `Admin` or `Moderator`

| Status | When |
| :--- | :--- |
| `200` | This call restored it; `ReviewRestored` recorded, rating recomputed |
| `409` | Not hidden - including restored by a concurrent call. `detail` "This review is not hidden." |
| `404` | No such review |
