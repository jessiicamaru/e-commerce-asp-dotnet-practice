# HTTP contract: a seller's returns

## `GET /api/orders/sales/returns` (Seller)

The request goes through the gateway's existing `/api/orders/{**catch-all}` route.

| Query | Default | Meaning |
| :-- | :-- | :-- |
| `status` | none (all) | One of `Requested`, `Accepted`, `Refused`, `Escalated`, `Rejected`, `SentBack`, `Received` |
| `page` | 1 | |
| `pageSize` | 12 | 1-50 |

The response is 200 with the same `ReturnPage` as staff's `GET /api/orders/returns`:

```json
{
  "items": [
    {
      "id": "…", "orderId": "…", "shipmentId": "…", "isShop": false, "status": "Requested",
      "reason": "Scratched lens", "decisionReason": null, "trackingReference": null,
      "requestedAt": "…", "decidedAt": null, "sentBackAt": null, "receivedAt": null, "refundAmount": null
    }
  ],
  "page": 1, "pageSize": 12, "totalCount": 1
}
```

Only returns of the caller's own parcels are listed, oldest waiting first (`UpdatedAt`).

| Answer | When |
| :-- | :-- |
| 400 | The status is unknown, or the page is out of range. |
| 401 | Anonymous. |
| 403 | The caller is not a seller. |

## `GET /api/orders/sales` (changed)

Each item gains `"returnStatus": "Requested" | … | null`: the state of the return of the caller's parcel of that order.
