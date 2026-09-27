# HTTP contract: A seller is told when a variant runs low

The existing gateway route `/api/stock/**` covers these. No gateway change.

## `PUT /api/stock/{variantId}/low-stock-threshold`: Seller, Admin (Inventory)

```json
{ "threshold": 10 }
```

`null` returns the variant to the shop default; `0` turns the notice off.

- `200` returns the stock response (below).
- `400` when `threshold` is below 0 or above 100,000 (`errors.Threshold`).
- `404` when the variant is another seller's or does not exist. It is the same response as `PUT /api/stock/{id}`
  gives (specs/031).
- `401` without a token; `403` for a customer.

## Stock responses (`GET /api/stock`, `GET /api/stock/{id}`, `PUT` responses)

Two fields are added:

```json
{
  "productId": "…", "sku": "FUJI-XT5", "quantityOnHand": 6, "quantityReserved": 0, "quantityAvailable": 6,
  "lowStockThreshold": 5,
  "lowStockThresholdIsDefault": true
}
```

`lowStockThreshold` is the effective value, and `lowStockThresholdIsDefault` says whether it is the shop's default.
