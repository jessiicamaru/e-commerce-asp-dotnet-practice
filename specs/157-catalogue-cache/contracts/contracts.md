# Contracts: The catalogue's public reads, served from memory

No HTTP shape changes: the same paths, bodies and headers. What changes is who answers an anonymous repeat:

| Endpoint | Cached for | Never cached |
| :-- | :-- | :-- |
| `GET /api/products` | anonymous 200s | a request with `Authorization`; non-200 |
| `GET /api/products/{id}` | anonymous 200s | the same; the 404 for an off-shelf product |
| `GET /api/categories` | anonymous 200s | the same |
| `GET /api/shops/{sellerId}` | anonymous 200s | the same |

Setting: `Caching:CatalogueSeconds` (env `Caching__CatalogueSeconds`), 30 by default, 0 = off, 1-600.
