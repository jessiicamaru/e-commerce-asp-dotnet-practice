# HTTP Contract: Payment Service

**Feature**: [spec.md](../spec.md)

Small by design — the service is driven by messages. These endpoints exist for FR-007 (account for a
payment) and FR-012 (make the configured outcome visible).

**Base**: `http://localhost:5061`, routed through the gateway at `/api/payments/{**catch-all}`.

Errors follow the project's RFC 7807 shape via `GlobalExceptionHandler`.

---

## `GET /api/payments/{orderId}` — `Admin` only

What was charged against an order.

```json
{
  "paymentId": "01a0aa10-...",
  "orderId": "01a0a9f9-...",
  "userId": "01a0a98f-...",
  "amount": 300000.00,
  "status": "Approved",
  "failureReason": null,
  "provider": "Stub",
  "processedAt": "2026-09-16T12:00:00Z"
}
```

`404` when the order has never been paid for — a plain answer rather than a failure (US2 scenario 2).

`provider` is always present and always `Stub`. It is the field that stops a record being mistaken
for evidence that money moved.

**Admin only**: a payment record names a user and an amount. It is not shopper-facing data, and the
project's rule is that endpoints are authenticated unless there is a reason not to be.

---

## `GET /api/payments` — `Admin` only

Paged list, same shape per item. Query: `pageNumber`, `pageSize`, `orderId`, `status`.

Mirrors the paging convention used by `GET /api/products` and `GET /api/stock`.

---

## `GET /health` — anonymous

Same JSON shape as the other services, plus the two facts FR-012 requires:

```json
{
  "status": "Healthy",
  "service": "Payment Service",
  "provider": "Stub - no money is moved",
  "configuredOutcome": "Approve",
  "checks": [ { "name": "payment_postgres_db", "status": "Healthy" } ]
}
```

`configuredOutcome` is what keeps a service deliberately set to `Reject` from being mistaken for a
broken one, and `provider` is what keeps a healthy stub from being mistaken for a real gateway.

---

## Authorization

| Endpoint | Access |
| :--- | :--- |
| `GET /api/payments`, `GET /api/payments/{orderId}` | `Admin` |
| `GET /health` | Anonymous |

Wired with `AddJwtAuthentication(builder.Configuration)` from `Ecommerce.Shared`, and the service
reads `JWT_SECRET` from the environment like every other service.

---

## Gateway routes

Added to `Ecommerce.ApiGateway/appsettings.json`: a route, a cluster, and a health route rewriting
`/api/payment/health` to `/health`, following the existing entries.
