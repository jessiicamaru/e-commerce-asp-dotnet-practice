# HTTP Contract: Staff find any order

**Feature**: [spec.md](../spec.md)

## `GET /api/orders/staff` - `Admin`

Through the gateway (`/api/orders/**`, no new route needed).

| Query | Type | Rule |
| :-- | :-- | :-- |
| `status` | string, optional | one of `Submitted`, `Paid`, `Preparing`, `Shipped`, `Failed`, `Cancelled`; `Paid` includes `Completed` |
| `search` | string, optional | the start of an order id: 4-36 of `[0-9a-fA-F-]`, case-insensitive |
| `customerId` | Guid, optional | one customer's orders |
| `page` | int, default 1 | ≥ 1 |
| `pageSize` | int, default 12 | 1-50 |

**200**

```json
{
  "items": [
    {
      "orderId": "01a0dd2b-...", "userId": "01a0...", "totalAmount": 1155000, "status": "Failed",
      "failureReason": "Card declined", "itemCount": 1, "createdAt": "2026-09-26T08:00:00Z", "currency": "VND",
      "shipmentCount": 1, "shipmentsShipped": 0
    }
  ],
  "page": 1, "pageSize": 12, "totalCount": 1
}
```

| Status | When |
| :-- | :-- |
| 400 | a status, search, page or page size outside the rules, named in `errors` |
| 401 | no token |
| 403 | any role but Admin |

## Used by the storefront with (unchanged)

- `GET /api/users?search=` (Admin, Moderator) - an email to a person.
- `GET /api/users/lookup?ids=` (Admin) - names and emails for the rows.

## Bruno

`order/` seq 12 "staff find the order by the start of its id" (200, the order in it) and seq 13 "a customer cannot search
every order" (403).
