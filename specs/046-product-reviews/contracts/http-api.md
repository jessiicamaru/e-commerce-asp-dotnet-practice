# HTTP Contract: Ratings and reviews

> Written on 2026-09-27, after the feature merged (#98), from the code at that merge, the pull request and docs/features/ratings-and-reviews.md.

**Feature**: [spec.md](../spec.md)

All endpoints are Catalog's (`ReviewsController`, `[Route("api")]`), reached through the gateway on `:5000`.
Errors are RFC 7807 ProblemDetails from `GlobalExceptionHandler`: `ValidationException` → 400 with `errors`,
`ForbiddenException` → 403 with its sentence in `detail`, `NotFoundException` → 404, `ConflictException` → 409.
Pages use Catalog's `PaginatedList`: `{ items, pageNumber, totalPages, totalCount, hasPreviousPage,
hasNextPage }`.

### The review object (`ReviewResponse`)

```json
{
  "id": "0199...",
  "productId": "0199...",
  "authorName": "Lan",
  "rating": 4,
  "body": "Sharp, a little loud",
  "createdAt": "2026-09-24T02:10:00Z",
  "updatedAt": "2026-09-24T02:12:00Z",
  "edited": true,
  "productName": null,
  "hiddenAt": null,
  "hiddenReason": null
}
```

`edited` is true when `updatedAt` is more than a second after `createdAt`. `productName` is filled only in the
staff list. `hiddenAt` / `hiddenReason` are filled on a hidden review, which only staff can read; the public
list never contains one.

---

## `GET /api/products/{productId}/reviews` - anonymous

A product's **visible** reviews, newest first (`CreatedAt` descending, then `Id`).

Query: `pageNumber` (default 1, > 0), `pageSize` (default 12, 1 to 50).

- `200` - a page of review objects. An unknown product id is an empty page, not a 404, at this merge (specs/081
  later made a product off the shelf a 404 here for everybody but its seller and staff).
- `400` - `pageNumber` or `pageSize` out of range.

---

## `GET /api/products/{productId}/reviews/mine` - signed in (`[Authorize]`)

Whether the caller may review the product, and their review if they wrote one.

```json
{ "eligible": true, "review": null }
```

- `200` always for a signed-in caller. `eligible` is whether `review_eligibility` holds (product, caller).
  `review` is the caller's review, visible or hidden, or null.
- `401` - no token.

---

## `PUT /api/products/{productId}/reviews/mine` - `Customer`

Writes the caller's review, or edits it.

```json
{ "rating": 5, "body": "Sharp and quiet" }
```

The body carries the rating and the words and **nothing else**: no customer id, no name (Principle IV).

- `200` - the review object. The first write creates it (`ReviewPosted` audit entry; `NewReview` notice to the
  product's seller, if it has one); every later write edits the same row (`ReviewEdited`, no notice). Both
  recompute the product's `ratingAverage` and `ratingCount` in the same transaction.
- `400` - `rating` outside 1-5 (`"Rating must be from 1 to 5 stars."`) or `body` over 2000 characters.
- `401` - no token. `403` - a token without the `Customer` role.
- `403` - the caller has not received the product:
  `{ "status": 403, "title": "Forbidden", "detail": "Only a customer who has received this product can review it." }`
- `404` - `Product with ID '{productId}' was not found.`

---

## `GET /api/reviews` - `Admin`, `Moderator` (`StaffRoles.Staff`)

Every product's reviews, newest first, with the product's name.

Query: `hidden` (`false` by default - the visible ones; `true` - the hidden ones, with `hiddenAt` and
`hiddenReason`), `pageNumber` (default 1), `pageSize` (default 12). No validator bounds the paging here, unlike
the public list.

- `200` - a page of review objects with `productName`.
- `401` / `403` - no token / not staff.

---

## `POST /api/reviews/{id}/hide` - `Admin`, `Moderator`

```json
{ "reason": "Advertising" }
```

- `200` - the review object, `hiddenAt` set, `hiddenReason` the trimmed reason. The product's average and count
  are recomputed without it. Audit: `ReviewHidden`, category `Moderation`, before `{ Hidden: false }`, after
  `{ Hidden: true, Reason }`.
- `400` - reason blank (`"Reason is required."`) or over 500 characters.
- `404` - `Review not found.`
- `409` - `This review is already hidden.`

---

## `POST /api/reviews/{id}/restore` - `Admin`, `Moderator`

No body.

- `200` - the review object with `hiddenAt` and `hiddenReason` null; the average and count include it again.
  Audit: `ReviewRestored`, category `Moderation`.
- `404` - `Review not found.`
- `409` - `This review is not hidden.`

---

## Changed response: products

`ProductResponse` (every product read and listing) gained two fields:

| Field | Type | Meaning |
| :-- | :-- | :-- |
| `ratingAverage` | number or null | Average of the visible reviews, 2 decimal places; null with no visible reviews |
| `ratingCount` | integer | Number of visible reviews |

Additive: an older client ignores them.

---

## Changed token: `given_name`

Identity's `JwtTokenGenerator` adds the `given_name` claim (`JwtRegisteredClaimNames.GivenName`, the user's
`FirstName`) to every access token. `ICurrentUser.GivenName` reads it; its default implementation returns null,
so a token issued before the change still validates and its review is signed with the email's first letter and
a dot.

---

## Authorization

| Endpoint | Access |
| :-- | :-- |
| `GET /api/products/{id}/reviews` | Anonymous (`[AllowAnonymous]`) |
| `GET /api/products/{id}/reviews/mine` | Any signed-in caller |
| `PUT /api/products/{id}/reviews/mine` | `Customer` role, then eligibility (403 in words) |
| `GET /api/reviews`, `POST /api/reviews/{id}/hide`, `/restore` | `Admin` or `Moderator` |

A seller holds `Customer` too (specs/027), so reaches the eligibility check like any customer; at this merge
nothing stopped a seller who received their own product from reviewing it (specs/057 added that 403).

## Gateway routes

Added to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`, both on `catalog-cluster`:

| Route | Match |
| :-- | :-- |
| `catalog-reviews-route` | `/api/reviews/{**catch-all}` |
| `catalog-reviews-root-route` | `/api/reviews` |

The product-scoped addresses already went to Catalog through the existing `/api/products/{**catch-all}` route.

## Unchanged, relied on

`POST /api/orders/{id}/shipments/{shipmentId}/received` (Order, specs/040, owner only) - the confirmation that
makes a customer eligible. Its request and responses did not change; it now also stages `ParcelDeliveredEvent`.
