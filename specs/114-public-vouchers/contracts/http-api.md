# Contract: public vouchers

## `GET /api/vouchers/public` - anonymous

| Query | Meaning |
| :-- | :-- |
| `platform=true` | Include the platform's public vouchers. |
| `sellerId` (repeatable, up to 20) | Include each shop's public vouchers. |
| `productId` | Only vouchers for everything, or targeting this product... |
| `variantId` (repeatable, up to 50) | ...or one of these variants. |

At least one of `platform` and `sellerId` - otherwise 400. The currency is the request's (`?currency=`, `X-Currency`).

```json
[
  {
    "code": "MAI10",
    "name": "Mai ten",
    "isPlatform": false,
    "sellerId": "…",
    "benefit": "Percent",
    "percent": 10,
    "currency": "VND",
    "fixedValue": null,
    "maxDiscount": 100000,
    "minSubtotal": 500000,
    "endsAt": "2026-10-31T17:00:00Z",
    "conditions": [ { "type": "FirstOrderInShop", "value": null } ],
    "targeted": true
  }
]
```

`targeted` says the voucher names particular products (without listing them). At most 12, ending soonest first. No
`totalLimit`, `usedCount` or `perCustomerLimit`, ever.

## Changed

- `POST /api/vouchers` and `PUT /api/vouchers/{id}` take `isPublic` (default false on create; on the edit, the new value).
- `VoucherSummary` (the owner's list) carries `isPublic`.
- `OrderItemResponse` (quote and order lines) carries `sellerId`, null for the shop's own goods.
