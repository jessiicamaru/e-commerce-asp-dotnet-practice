# HTTP Contract: Admin revenue less returns

> Written on 2026-09-27, after the feature merged (#176), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](../spec.md)

**No request or response shape changed.** Three `Admin` endpoints of Order, through the gateway on `:5000`, now
return different numbers for periods that contain a received return. No message or gRPC contract changed.

| Endpoint | Field | Before | After |
| :--- | :--- | :--- | :--- |
| `GET /api/orders/insights/revenue` | `totals[].revenue`, `days[].revenue` | Σ `TotalAmount` of sold orders | the same less Σ `RefundAmount` of their received returns, per day and currency |
| `GET /api/orders/insights/revenue` | `totals[].orders`, `days[].orders`, `averageOrderValue` | order count | order count unchanged; `averageOrderValue` is now net revenue ÷ orders, rounded to 2 places half away from zero |
| `GET /api/orders/insights/top-products` | the list | every line of sold orders | without the lines of a parcel whose return was received |
| `GET /api/orders/insights/top-buyers` | `spent[].amount` | Σ `TotalAmount` per currency | the same less their refunds per currency; `orders` unchanged |

Query parameters (`from`, `to`, `currency`, `by`, `limit`), authorization (`Admin`), status codes and the period
rule (specs/055, 082) are unchanged. The seller's endpoints (`/api/orders/sales/insights/*`) already left returned
parcels out (specs/068) and did not change.
