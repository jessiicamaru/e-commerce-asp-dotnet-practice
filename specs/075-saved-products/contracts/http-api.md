# HTTP Contract: A shopper saves a product for later

> Written on 2026-09-27, after the feature merged (#159), from the code at that merge, the pull request and docs/features/saved-products.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

Four endpoints in Catalog, all in
[SavedProductsController.cs](../../../server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/SavedProductsController.cs),
`[Route("api/products")]` with `[Authorize]` on the class. Every one is about **the caller's own list**: whose it
is comes from the token through `ICurrentUser` (Constitution IV), and no endpoint takes a shopper id.

**Base**: Catalog on `http://localhost:5057`, reached through the gateway at `http://localhost:5000`. The existing
`catalog-products-route` (`/api/products/{**catch-all}`) carries all four; no gateway route was added.

**Route order**: `saved` and `saved/ids` do not collide with the product lookup's `{id:guid}`, because `saved` is
not a guid.

Errors follow the project's RFC 7807 shape via `GlobalExceptionHandler` - see
[error-handling-and-shared-building-block.md](../../../docs/architecture/error-handling-and-shared-building-block.md).
Every endpoint answers **401** without a valid token (the `[Authorize]` attribute), and 401 too if the token
carries no usable user id (`UnauthorizedAccessException` from the handler's `Caller()`).

---

## `PUT /api/products/{id}/saved` - signed in

Save the product. No body. `SaveProductCommand(ProductId)`.

| Status | When |
| :-- | :-- |
| `204 No Content` | Saved - or already saved, which changes nothing (the first `SavedAt` is kept) |
| `404 Not Found` | No such product, **or** a product that is not on sale: not approved (`IsListed` false) or not active. Body `detail`: `Product not found.` - the same answer as an id that does not exist, so a hidden product's id is not confirmed (research D2) |
| `401 Unauthorized` | No token |

Written as one `INSERT ... ON CONFLICT ("CustomerId", "ProductId") DO NOTHING` (research D5).

---

## `DELETE /api/products/{id}/saved` - signed in

Unsave the product. No body. `UnsaveProductCommand(ProductId)`.

| Status | When |
| :-- | :-- |
| `204 No Content` | Removed, or it was never saved, or no such product exists - unsaving is idempotent and never an error |
| `401 Unauthorized` | No token |

The delete is guarded on the caller's id, so it can only ever remove the caller's own row.

---

## `GET /api/products/saved?page=&pageSize=` - signed in

The caller's saved products, newest first (`SavedAt` descending, then `ProductId`). `GetSavedProductsQuery(Page
= 1, PageSize = 12)`; the defaults match the storefront's `PAGE_SIZE`.

**Query**:

| Parameter | Default | Rule |
| :-- | :-- | :-- |
| `page` | `1` | `>= 1` |
| `pageSize` | `12` | `1` to `50` |

A value outside the rule is **400** with the validator's `errors` extension (`GetSavedProductsQueryValidator`).

**Headers**: the product text is in the request's language (`?lang=`, then `Accept-Language`) and the prices in
its currency (`?currency=`, then `X-Currency`), negotiated service-wide exactly as for the listing (specs/021,
specs/022).

**200**:

```json
{
  "items": [
    {
      "product": { "id": "0199...", "name": "...", "price": 45990000, "availability": "InStock", "...": "..." },
      "savedAt": "2026-09-26T09:58:12.345Z",
      "available": true
    }
  ],
  "pageNumber": 1,
  "totalPages": 1,
  "totalCount": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

- `product` is the listing's own `ProductResponse` (built by `ProductResponse.From`, with the shop name from the
  `sellers` read model), read **now** - today's price, today's name - not a copy from when it was saved
  (research D6). A variant not priced in the requested currency has a null price, as in the listing.
- `savedAt` is when it was first saved.
- `available` is `IsListed && IsActive && Availability`: on sale, active and in stock. A product withdrawn,
  rejected or taken down since it was saved **stays** in the list with `available: false` (research D2); a
  product deleted outright is gone (the cascade).
- The page shape is Catalog's `PaginatedList<T>`. The storefront's `SavedPage` type reads `items`, `pageNumber`,
  `totalPages` and `totalCount`.

The example values are illustrative; the shape is from the code and the Bruno test
`the saved list reads it as the listing does`, which asserts each item has exactly the keys `product`, `savedAt`
and `available`.

---

## `GET /api/products/saved/ids` - signed in

The caller's saved product ids alone, newest first. `GetSavedProductIdsQuery`.

**200**:

```json
["0199a1b2-...", "0199a0c3-..."]
```

Exists so the storefront can draw every heart on a page of cards with one request (research D7). A product saved
and later taken down is still in this list.

---

## Authorization

| Endpoint | Access |
| :-- | :-- |
| `PUT /api/products/{id}/saved` | Any signed-in caller - the caller's own list |
| `DELETE /api/products/{id}/saved` | Any signed-in caller - the caller's own list |
| `GET /api/products/saved` | Any signed-in caller - the caller's own list |
| `GET /api/products/saved/ids` | Any signed-in caller - the caller's own list |

No role is required (`[Authorize]` with no roles): any signed-in caller saves to, and reads, only their own
list. There is no staff read of somebody else's list.

---

## Bruno

`bruno/product/`, run with the rest of the `product` folder (`customerToken`, `productId` carried from earlier
requests):

| seq | Request | Asserts |
| :-- | :-- | :-- |
| 61 | `a customer saves the product` - `PUT /api/products/{{productId}}/saved` | 204 |
| 62 | `saving it again changes nothing` - the same `PUT` | 204 |
| 63 | `the saved ids hold it once` - `GET /api/products/saved/ids` | 200, the product once |
| 64 | `the saved list reads it as the listing does` - `GET /api/products/saved?page=1&pageSize=12` | 200, keys `product`, `savedAt`, `available` |
| 65 | `the customer unsaves it` - `DELETE /api/products/{{productId}}/saved` | 204 |
| 66 | `saved products without a token is 401` - `GET /api/products/saved`, no auth | 401 |
