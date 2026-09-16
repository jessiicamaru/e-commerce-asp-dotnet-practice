# HTTP Contract: Inventory Reservations

**Feature**: [spec.md](../spec.md)

A small API — the service's real work arrives over the message bus. These endpoints exist to
satisfy FR-012 (inspect stock) and FR-015 (set stock), plus the health check every service has.

**Base**: `http://localhost:5060`, routed through the gateway at `/api/inventory/{**catch-all}`.

Errors follow the project's RFC 7807 shape via `GlobalExceptionHandler` — see
[error-handling-and-shared-building-block.md](../../../docs/architecture/error-handling-and-shared-building-block.md).

---

## `GET /api/stock/{productId}` — anonymous

Inspect current stock for one product.

```json
{
  "productId": "01a0a98b-0000-7000-8000-000000000001",
  "sku": "SKU-1001",
  "quantityOnHand": 10,
  "quantityReserved": 3,
  "quantityAvailable": 7
}
```

`404` if the product is not registered — distinct from a registered product with zero units, which
returns `200` with zeroes. The spec's edge cases depend on that distinction being visible.

---

## `GET /api/stock` — anonymous

Paged list, same shape per item. Query: `pageNumber`, `pageSize`, `sku`.

Mirrors the paging convention already used by `GET /api/products`.

---

## `PUT /api/stock/{productId}` — `Admin` only

Set the quantity on hand (FR-015).

```json
{ "quantityOnHand": 50 }
```

**Rules**:

- Absolute value, not a delta. A delta endpoint invites lost updates from concurrent callers.
- Rejected with `409` if the new value would fall below `QuantityReserved` — that would break the
  table's check constraint and, more importantly, mean promising units that no longer exist. The
  message says how many are currently reserved.
- `404` if the product is not registered. Stock cannot be set for something inventory has never
  heard of; registration comes from `ProductCreatedEvent`.
- Takes the same row lock as the reserve path, so it cannot interleave with a reservation.

Returns the updated stock, same shape as `GET`.

---

## `GET /api/reservations/{orderId}` — `Admin` only

Which units an order is holding, and in what state (FR-010). Exists so that a stuck order can be
diagnosed without database access.

```json
{
  "orderId": "01a0a98f-...",
  "reservations": [
    {
      "productId": "01a0a98b-...",
      "quantity": 3,
      "status": "Held",
      "expiresAt": "2026-09-16T10:15:00Z",
      "createdAt": "2026-09-16T10:00:00Z",
      "settledAt": null,
      "settlementReason": null
    }
  ]
}
```

`200` with an empty list for an order with no reservations — asking about an unknown order is not
an error.

---

## `GET /health` — anonymous

Same JSON shape as the other services: overall status plus the database and bus checks.

---

## Authorization

| Endpoint | Access |
| :--- | :--- |
| `GET /api/stock`, `GET /api/stock/{id}` | Anonymous — the storefront shows availability |
| `PUT /api/stock/{id}` | `Admin` |
| `GET /api/reservations/{orderId}` | `Admin` |
| `GET /health` | Anonymous |

Wired with `AddJwtAuthentication(builder.Configuration)` from `Ecommerce.Shared`, matching Catalog.
The service reads `JWT_SECRET` from the environment like every other service — omitting that is the
documented way to have every token rejected.

---

## Gateway routes

Added to `Ecommerce.ApiGateway/appsettings.json`: a route plus a cluster, and a health route
rewriting `/api/inventory/health` to `/health`, following the existing entries exactly.
