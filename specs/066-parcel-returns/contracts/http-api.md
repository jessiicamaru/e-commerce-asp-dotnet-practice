# HTTP Contract: Returning a delivered parcel

> Written on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature**: [spec.md](../spec.md)

All on the Order service, in `ReturnsController` (`[Authorize]`, route prefix `api/orders`), through the gateway's
existing `/api/orders/{**catch-all}` route on `:5000`. No gateway change.

Errors are RFC 7807 ProblemDetails from `GlobalExceptionHandler`: `ValidationException` → 400 with `errors`,
`NotFoundException` → 404, `ConflictException` → 409. A missing token is 401; a token without the route's role is 403.

Every step answers **200** with the return as it now stands:

```json
{
  "id": "01a0d7d5-…",
  "orderId": "…",
  "shipmentId": "…",
  "isShop": true,
  "status": "Requested",
  "reason": "The lens has a scratch across the front element",
  "decisionReason": null,
  "trackingReference": null,
  "requestedAt": "2026-09-25T09:00:00Z",
  "decidedAt": null,
  "sentBackAt": null,
  "receivedAt": null,
  "refundAmount": null
}
```

`isShop` is true when the parcel is the shop's own (`SellerId` null). `status` is one of `Requested`, `Accepted`,
`Refused`, `Escalated`, `Rejected`, `SentBack`, `Received`.

---

## The buyer - any signed-in user, scoped to their own order

### `POST /api/orders/{id}/shipments/{shipmentId}/return`

Body `{ "reason": "…" }` - required, at most 1000 characters.

| Status | When |
| :-- | :-- |
| 200 | Return recorded as `Requested` |
| 400 | No reason ("Say why you want to return it.") |
| 404 | `Parcel not found.` - not the caller's order, or no such parcel |
| 409 | Cancelled order, parcel not `Shipped` or not delivered: "Only a delivered parcel can be returned." |
| 409 | Delivered 7 days ago or more: "A parcel can be returned within 7 days of its delivery; this one arrived on …" |
| 409 | "This parcel already has a return." |

### `POST /api/orders/{id}/shipments/{shipmentId}/return/escalate`

No body. `Refused` → `Escalated`, within 7 days of the refusal.

| Status | When |
| :-- | :-- |
| 404 | `Parcel not found.`, or `This parcel has no return.` |
| 409 | Not refused ("Only a refused return can be taken further."), or the refusal is too old |

### `POST /api/orders/{id}/shipments/{shipmentId}/return/sent`

Body `{ "trackingReference": "…" }` - required, at most 100 characters. `Accepted` → `SentBack`, within 7 days of the
acceptance.

| Status | When |
| :-- | :-- |
| 400 | No tracking reference |
| 404 | As above |
| 409 | Not accepted, or the acceptance lapsed ("An accepted return must be sent back within 7 days.") |

---

## The seller - `[Authorize(Roles = "Seller")]`, their part of the order read from the token

### `POST /api/orders/sales/{id}/return/accept`

No body. `Requested` → `Accepted`.

### `POST /api/orders/sales/{id}/return/refuse`

Body `{ "reason": "…" }` - required for a refusal, at most 500 characters. `Requested` → `Refused`.

### `POST /api/orders/sales/{id}/return/received`

No body. `SentBack` → `Received`; publishes `ParcelReturnedEvent`; `refundAmount` is set.

| Status | When |
| :-- | :-- |
| 400 | A refusal with no reason ("A refusal needs a reason the buyer can read.") |
| 403 | Caller does not hold `Seller` |
| 404 | `Sale not found.` (the caller has no part in that order), or `This parcel has no return.` |
| 409 | The return is not in the state the step expects - including an escalated return, which is staff's |

---

## Staff - `[Authorize(Roles = "Admin")]`

### `POST /api/orders/fulfilment/{id}/shipments/{shipmentId}/return/accept`
### `POST /api/orders/fulfilment/{id}/shipments/{shipmentId}/return/refuse` `{ "reason": "…" }`

For an **escalated** return of any parcel: `Escalated` → `Accepted`, or → `Rejected` (final). Otherwise, for the
**shop's own** parcel only: `Requested` → `Accepted` / `Refused`.

409 for a seller's parcel that is not escalated: "A seller answers a return of their own parcel; staff decide once the
buyer escalates it."

### `POST /api/orders/fulfilment/{id}/shipments/{shipmentId}/return/received`

The shop's own parcel only: `SentBack` → `Received`. 409 for a seller's parcel: "A returned parcel goes back to its
seller, who marks it received."

### `GET /api/orders/returns?status=&page=1&pageSize=12`

Returns by state, the longest waiting (`UpdatedAt`) first. `status` optional, one of the seven names
(case-insensitive); `pageSize` 1-50.

```json
{ "items": [ { "id": "…", "status": "Escalated", "…": "…" } ], "page": 1, "pageSize": 12, "totalCount": 1 }
```

400 for an unknown status; 403 for anyone but an administrator.

---

## Read models that gained a field

| Response | Field | Where it is served |
| :-- | :-- | :-- |
| `ShipmentResponse` | `return` (the object above, or null) | `GET /api/orders/{id}` (the buyer), `GET /api/orders/fulfilment/{id}` (Admin) |
| `SaleDetailResponse` | `return` (or null) | `GET /api/orders/sales/{id}` (the seller) |

Both fields are optional additions; an older client ignores them.
