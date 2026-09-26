# HTTP Contract: Insights in the shop's days

> Written on 2026-09-27, after the feature merged (#170), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](../spec.md)

No endpoint was added or removed, and no route or authorization changed. The meaning of the period changed for
every insight, and one response gained two fields. No message or gRPC contract changed.

---

## The period, for every insight

Applies to:

| Endpoint | Service | Access |
| :--- | :--- | :--- |
| `GET /api/orders/insights/revenue` | Order | `Admin` |
| `GET /api/orders/insights/top-products` | Order | `Admin` |
| `GET /api/orders/insights/top-buyers` | Order | `Admin` |
| `GET /api/orders/sales/insights/revenue` | Order | `Seller` |
| `GET /api/orders/sales/insights/top-products` | Order | `Seller` |
| `GET /api/products/insights/top-viewed` | Catalog | `Admin` |
| `GET /api/products/insights/mine` | Catalog | `Seller` |

Query `from`, `to` (optional). Rules:

- Each end is snapped to **its day in the shop's zone** (`Insights:TimeZone`, `Asia/Ho_Chi_Minh`). A value with a
  zone or `Z` is converted; a bare date (`2026-09-26`) is that day as it is.
- Whole days, both ends included; the last 30 days, today included, by default; at most 366; `from` not after `to`.
  Unchanged from specs/055, including the `400` and its words (`The period must not start after it ends.`).

## `GET /api/orders/insights/revenue` and `GET /api/orders/sales/insights/revenue` - response

```json
{
  "from": "2026-08-27T17:00:00Z",
  "to": "2026-09-26T17:00:00Z",
  "totals": [ { "currency": "VND", "revenue": 1250000, "orders": 3, "averageOrderValue": 416666.67 } ],
  "days": [ { "day": "2026-09-25", "currency": "VND", "revenue": 1250000, "orders": 3 } ],
  "firstDay": "2026-08-28",
  "lastDay": "2026-09-26"
}
```

| Field | Change |
| :--- | :--- |
| `from`, `to` | Same meaning (UTC instants, `to` exclusive), now the shop's midnights: in Hanoi `from + 7 h` is `firstDay` |
| `days[].day` | The shop's date the order was paid on |
| `firstDay`, `lastDay` | **New.** The first and last shop days of the period, both included. The chart draws exactly these |

Additive: a client that ignores the new fields keeps working. Bruno's seller request asserted the exact keys, and
was updated in the same change to include them - the one consumer that noticed.

## `POST /api/products/{id}/view` - anonymous

Unchanged in shape and status (always `204`); the view is recorded on the shop's today.
