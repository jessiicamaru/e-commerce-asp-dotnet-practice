# Contracts: A product inserted without a review status waits for review

**Feature**: [spec.md](../spec.md)

**No HTTP, message or gRPC contract changes.** Every endpoint answers as before for everything the current image
writes. The change is a column default, which only an earlier image's INSERT reaches (see
[data-model.md](../data-model.md)).

What an earlier image's product now looks like to the current API, for the record:

| Request | Answer for a product inserted without `ReviewStatus` |
| :-- | :-- |
| `GET /api/products/{id}` as a shopper | 404 (was 200) |
| `GET /api/products` | not listed (was listed) |
| `GET /api/products/review?status=Pending` (Staff) | listed in the queue (was absent) |
| `POST /api/products/{id}/approve` (Staff) | 200, now on sale |
| gRPC `CatalogPricing` | `Sellable = false` until approved (was true) |

## Bruno

No change: the collection runs against the current image, which always writes the column.
