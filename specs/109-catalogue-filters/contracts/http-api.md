# HTTP contract: catalogue filters

## `GET /api/products` (anonymous) - three new query parameters

| Parameter | Type | Meaning |
| :-- | :-- | :-- |
| `minPrice` | decimal ≥ 0 | The "from" price is at least this, in the request's currency (`?currency=` / `X-Currency`). |
| `maxPrice` | decimal ≥ 0 | The "from" price is at most this. |
| `inStock` | bool | `true` lists only products that can be bought now. |

- The parameters combine with `categoryId`, `searchTerm`, `sellerId` and `sortBy`, using AND.
- A product with no price in the request's currency is excluded whenever `minPrice` or `maxPrice` is given.

| Answer | When |
| :-- | :-- |
| 200 | The usual page of products. |
| 400 | A negative bound, or `minPrice > maxPrice`. The error names the field. |

Example: `GET /api/products?maxPrice=20000000&inStock=true&currency=VND`.
