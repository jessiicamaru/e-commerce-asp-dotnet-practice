# Contracts: A shop's rating

Through the gateway's existing `/api/shops/**` route. No message, no gRPC change.

## `GET /api/shops/{id}` - anonymous, cached (specs/157); two fields added

```json
{
  "sellerId": "…",
  "shopName": "Mai Lens",
  "description": "…",
  "productCount": 12,
  "paused": false,
  "ratingAverage": 4.62,
  "ratingCount": 318
}
```

- `ratingAverage`: `null` when no product of the shop has a visible review; otherwise the average of those reviews, two
  decimals.
- `ratingCount`: how many reviews it rests on; 0 with a null average.
- Additive: a client that does not read them is unaffected. 404s unchanged (unknown, unnamed, banned, closed).

## `GET /api/products/insights/mine` - unchanged shape

Its `ratingAverage` / `ratingCount` now come from the same method as the shop page's.
