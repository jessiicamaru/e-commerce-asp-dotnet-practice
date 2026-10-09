# Contracts: A gallery of photographs per product

All through the gateway's existing `/api/products/**` route. No message, no gRPC change.

## Product responses

`ProductResponse` gains:

```json
"photos": [
  { "id": "0199...", "url": "/api/products/{id}/photos/{photoId}?k=..." }
]
```

- In gallery order, **after** the cover (`imageUrl`, unchanged).
- Filled by `GET /api/products/{id}` and by `GET /api/products/review` (staff); **`null`** on the listing and other
  reads.
- `k` is present only for a product off the shelf; the address is given only to whoever may read the product.

## Writes - Seller (own, else 404) or Admin; a seller's change to an approved product sends it back to review

| Method | Path | Body | Answer |
| :-- | :-- | :-- | :-- |
| `POST` | `/api/products/{id}/photos` | multipart, one part `file` (≤ 2 MB, JPEG/PNG/WebP) | 200 `ProductResponse` with `photos`. With no cover, the file becomes the cover. 400 not an image / too large / empty; 409 the gallery holds 10 |
| `DELETE` | `/api/products/{id}/photos/{photoId}` | - | 204. 404 not this product's |
| `POST` | `/api/products/{id}/photos/{photoId}/cover` | - | 200 `ProductResponse`: the photograph is the cover, the old cover in its place. 404 not this product's; 409 the cover changed meanwhile |
| `PUT` | `/api/products/{id}/photos/order` | `{ "photoIds": ["...", "..."] }` | 200 `ProductResponse`. 400 when the list is not exactly the current photographs, each once |

`DELETE /api/products/{id}/image` (existing) now promotes the first photograph when there is one, instead of leaving a
gallery with no cover.

## Read - anyone

| Method | Path | Answer |
| :-- | :-- | :-- |
| `GET` | `/api/products/{id}/photos/{photoId}?k=` | 200 bytes, `Content-Type` from the row, `nosniff`; `public, max-age=31536000, immutable` on a product on sale, `private, no-store` otherwise. 404 unknown, not this product's, or off the shelf without the photograph's own `k` |

## Audit actions (Catalog category)

`ProductPhotoAdded`, `ProductPhotoRemoved`, `ProductPhotosReordered`, `ProductCoverChosen`.
