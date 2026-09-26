# HTTP Contract: A seller sees how their shop is doing

> Written on 2026-09-27, after the feature merged (#152), from the code at that merge, the pull request and
> docs/features/seller-insights.md.

**Feature**: [spec.md](../spec.md)

Three read-only endpoints, all `[Authorize(Roles = "Seller")]`, through the gateway on `:5000` by the existing
`/api/orders/**` and `/api/products/**` routes. **No seller id in any request**: the token's subject is the seller.
No message and no gateway change.

**The period** (all three): `from` and `to` are optional date-times; the period is whole UTC days, both ends included,
the last 30 days when neither is given, at most 366 days (`InsightsPeriod`, specs/055). A period that ends before it
starts or is longer than 366 days is **400** with the validator's message.

Errors: 401 without a token, 403 for a caller without `Seller`, 400 for a bad period or limit. A token without a user
id is refused (`UnauthorizedAccessException`).

---

## `GET /api/orders/sales/insights/revenue?from=&to=` - Order

The caller's own lines on sold orders, before tax, less any part whose return was received. The admin's shape
(`RevenueResponse`):

```json
{
  "from": "2026-08-28T00:00:00Z",
  "to": "2026-09-27T00:00:00Z",
  "totals": [ { "currency": "VND", "revenue": 3297800.00, "orders": 2, "averageOrderValue": 1648900.00 } ],
  "days":   [ { "day": "2026-09-25", "currency": "VND", "revenue": 3297800.00, "orders": 2 } ]
}
```

One total per currency; never summed across currencies. An order with several of the seller's lines counts once per
day and currency.

## `GET /api/orders/sales/insights/top-products?from=&to=&limit=10` - Order

`limit` 1-50, default 10. The caller's products ranked by units, with revenue per currency (`TopProduct[]`):

```json
[ { "productId": "…", "productName": "Fujifilm X-T5", "units": 3, "revenue": [ { "currency": "VND", "amount": 3297800.00 } ] } ]
```

## `GET /api/products/insights/mine?from=&to=&limit=10` - Catalog

`limit` 1-50, default 10. Views and ratings over the caller's products (`SellerProductInsights`):

```json
{
  "views": 42,
  "ratingAverage": 2.5,
  "ratingCount": 4,
  "products": [ { "productId": "…", "name": "…", "views": 30, "ratingAverage": 4.0, "ratingCount": 1 } ]
}
```

`views`, `ratingAverage` and `ratingCount` cover all the seller's products; `products` is the `limit` most viewed.
`ratingAverage` is weighted by review count, rounded to 2 places, and **null** - never 0 - with no reviews.

---

## Authorization

| Endpoint | Seller | Customer | Anonymous |
| :-- | :-- | :-- | :-- |
| all three | 200 | 403 (Bruno `seller/` 58, 59) | 401 |
