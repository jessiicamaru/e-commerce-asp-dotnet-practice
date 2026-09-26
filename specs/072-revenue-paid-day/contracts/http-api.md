# HTTP Contract: Revenue counts on the day an order was paid

> Written on 2026-09-27, after the feature merged (#156), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](../spec.md)

No endpoint was added, no route changed, no message changed. One response gained a field and five answers changed
meaning.

## A field added

`GET /api/orders/{id}` (the buyer) and the other reads that return `OrderDetailResponse`:

```json
{ "…": "…", "paidAt": "2026-09-26T08:59:12.345Z" }
```

`paidAt` is null while the order is settling, when it failed, and on orders from before this change. Optional, so an
older client ignores it.

## Answers whose dating changed

Same shapes, same auth; a sale is now dated by `paidAt ?? createdAt` for both the period filter and the day it is
grouped into:

| Endpoint | Who |
| :-- | :-- |
| `GET /api/orders/insights/revenue` | Admin |
| `GET /api/orders/insights/top-products` | Admin |
| `GET /api/orders/insights/top-buyers` | Admin |
| `GET /api/orders/sales/insights/revenue` | Seller |
| `GET /api/orders/sales/insights/top-products` | Seller |

## Messages relied on (unchanged)

`OrderCompletedEvent(OrderId, CompletedAt)` from the saga; its `CompletedAt` becomes `orders.PaidAt`.
