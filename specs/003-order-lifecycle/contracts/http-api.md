# HTTP Contracts: Order Lifecycle Visibility

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-16

Base: `http://localhost:5059/api/orders`, or `http://localhost:5000/api/orders` through the gateway.
Both new endpoints are `[Authorize]`; neither accepts a user id in any form.

---

## `GET /api/orders`

The caller's own orders, newest first.

### Query parameters

| Name | Type | Default | Rules |
| :--- | :--- | :--- | :--- |
| `page` | int | `1` | ≥ 1 |
| `pageSize` | int | `20` | 1–100 |

Out-of-range values are rejected by FluentValidation as 400 `ProblemDetails` with an `errors`
extension, the same as every other invalid request in this project. They are **not** silently
clamped: a caller who asks for 5000 items should be told they cannot have them, not handed 100 and
left to think that was all.

### 200

```json
{
  "items": [
    {
      "orderId": "01934f8e-...",
      "totalAmount": 259.98,
      "status": "Completed",
      "failureReason": null,
      "itemCount": 2,
      "createdAt": "2026-09-16T09:12:04.118Z",
      "updatedAt": "2026-09-16T09:12:07.402Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 37
}
```

The list carries `itemCount`, not the line items themselves — a shopper scanning their orders does
not need every product on every one, and loading them would make the list cost grow with order size
rather than with page size.

### 401

No token, an expired one, or one this service will not validate. The body is
`ProblemDetails`; no order data is read before this point.

### Notes

- An empty page is a **200 with an empty `items` array**, including for a page past the end.
  `totalCount` still reports the true total, which is how the caller knows they overshot.
- `totalCount` counts the caller's orders only.

---

## `GET /api/orders/{id}`

One of the caller's own orders, with its line items.

### 200

```json
{
  "orderId": "01934f8e-...",
  "userId": "01934a01-...",
  "totalAmount": 259.98,
  "status": "Failed",
  "failureReason": "Payment rejected by the configured outcome",
  "createdAt": "2026-09-16T09:12:04.118Z",
  "updatedAt": "2026-09-16T09:12:07.402Z",
  "items": [
    {
      "productId": "01934b77-...",
      "productName": "Mechanical Keyboard",
      "quantity": 2,
      "unitPrice": 129.99,
      "totalPrice": 259.98
    }
  ]
}
```

`userId` is echoed because it is always the caller's own — it identifies nobody the caller does not
already know about.

### 404

Returned when the order does not exist **and** when it exists but belongs to another shopper. The
two cases are indistinguishable by design (FR-010, research D5): the owner is part of the query, so
the handler never holds somebody else's order.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Resource not found",
  "status": 404,
  "detail": "Order not found."
}
```

The detail deliberately does not say "not yours".

### 401

As above.

---

## Unchanged

### `POST /api/orders`

Untouched. It still takes no `UserId` — the constitution IV property this feature must not regress.

### `GET /health`

Untouched.

---

## Gateway

The gateway already has `order-route` matching `/api/orders/{**catch-all}` against `order-cluster`,
so no new route or cluster is needed — this feature adds methods to an existing path, not a new
service.

One thing to **check rather than assume**: `GET /api/orders` with no trailing segment relies on the
catch-all matching zero segments. Route templates allow that, but nothing in this repository
currently exercises it — the existing `POST /api/orders` has only ever been called against the
service directly. The quickstart asserts it through the gateway for exactly this reason. If it turns
out not to match, the fix is a second route for the bare path, not a change to the endpoint.
