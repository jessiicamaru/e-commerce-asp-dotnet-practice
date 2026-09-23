# Contracts: What the shop owes each seller

All through the gateway's existing `/api/orders/{**}` route. No gRPC or message change.

## Seller (`Seller`)

### `GET /api/orders/sales` and `/sales/{id}` - additive fields

```json
{ "goodsTotal": 1200000, "commission": 120000, "shippingShare": 15000, "payout": 1095000, "paidOut": false }
```

All null when the terms were not recorded (orders before this).

### `GET /api/orders/sales/balance`

```json
[{ "currency": "VND", "onTheWay": 0, "due": 1095000, "paidOut": 2300000 }]
```

One row per currency the seller has earnings in.

### `GET /api/orders/sales/payouts?page=&pageSize=`

Paged (default 12): `{ items: [{ id, amount, currency, partCount, createdAt }], page, pageSize, totalCount }`.

## Administrator (`Admin`)

### `GET /api/orders/payouts/due`

```json
[{ "sellerId": "…", "sellerName": "Mai Lens Hà Nội", "currency": "VND", "due": 1095000, "parts": 1 }]
```

`sellerName` is the latest name frozen on that seller's lines, or null.

### `POST /api/orders/payouts`

Body `{ "sellerId": "…", "currency": "VND" }` → **201** `{ id, sellerId, currency, amount, partCount, createdAt }`.
**409** `Nothing is due to this seller in VND.` when no part is due - including a currency nothing was
ever sold in. **400** on a missing seller or a currency that is not three letters.
