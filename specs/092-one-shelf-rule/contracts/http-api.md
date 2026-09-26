# HTTP and gRPC Contract: One rule for "off the shelf"

**Feature**: [spec.md](../spec.md)

**No route, request or response shape changes.** For an approved product with `IsActive = false`, the answers change
to match a product that is not approved:

| Endpoint (through the gateway) | Before, for a withdrawn product | After |
| :-- | :-- | :-- |
| `GET /api/products/{id}` (anonymous or a shopper) | 200 | **404** |
| `GET /api/products` (listing, search) | included | **not included** |
| `GET /api/products/{id}/image` without `k` | served, publicly cacheable | **404** |
| `GET /api/products/{id}/image?k=<its key>` | served, publicly cacheable | served, **`Cache-Control: private`** |
| variant image, same two cases | same as the product's | same as the product's |
| `GET /api/products/{id}/reviews` | 200 | **404** |
| `GET /api/products/{id}/questions` | 200 | **404** |
| `POST /api/products/{id}/view` | counted | **not counted** (still 204) |
| writing a review, asking a question, saving | 404 | 404 (unchanged) |
| the product's seller, a moderator, an administrator | 200 | 200 (unchanged) |

gRPC `CatalogPricing` (`Sellable`) and `ProductVariant.Sellable`: unchanged in effect (they already required
`IsActive`); now written through `OnShelf`.

## Bruno

No request changes: no endpoint can withdraw a product, so the collection cannot reach the state. The Catalog tests
reach it by setting the column.
