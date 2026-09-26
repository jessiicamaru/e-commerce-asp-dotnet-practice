# HTTP Contract: In-app notifications

> Written on 2026-09-27, after the feature merged (#94), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

Four endpoints on the Activity service (`NotificationsController`, REST on 5063), all through the gateway on
`:5000`. **Every one reads the caller from the access token; none takes a user id** (research D7).

**Gateway routes** added to `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`, both to the existing
`activity-cluster` (specs/041):

| Route | Match |
| :-- | :-- |
| `notifications-root-route` | `/api/notifications` |
| `notifications-route` | `/api/notifications/{**catch-all}` |

The pair follows the pattern the file already used for `/api/addresses`, `/api/sellers`, `/api/audit` and
`/api/cart`: a root route beside the catch-all. Why the root route is needed was not recorded (`/api/orders`
and `/api/products` have only a catch-all).

**Authorization**: `[Authorize]` on the controller - any signed-in person, any role. No role reads another
person's inbox, an administrator included. Without a token every endpoint is **401**.

Errors are RFC 7807 ProblemDetails through `Ecommerce.Shared`'s `GlobalExceptionHandler`.

---

## `GET /api/notifications` - signed in

The caller's notifications, newest first (`CreatedAt` descending, then `Id` descending).

**Query** (bound to `GetMyNotificationsQuery`):

| Parameter | Default | Rule |
| :-- | :-- | :-- |
| `unreadOnly` | `false` | Only those with `readAt` null |
| `page` | `1` | `>= 1` |
| `pageSize` | `12` | `1`-`50` |

**200**:

```json
{
  "items": [
    {
      "id": "01999e3a-8c1e-7b40-9f0e-2a4c6d8e0f12",
      "kind": "OrderPaid",
      "data": { "orderId": "01999e39-...", "total": "22462000", "currency": "VND" },
      "link": "/orders/01999e39-...",
      "createdAt": "2026-09-23T20:10:04.512Z",
      "readAt": null
    }
  ],
  "page": 1,
  "pageSize": 12,
  "totalCount": 1
}
```

`totalCount` counts what the filter matches (with `unreadOnly=true`, the unread ones). The values in `data` are
always strings; which keys each kind carries is in [messages.md](./messages.md).

**400** with an `errors` extension for `page < 1` or `pageSize` outside 1-50. **401** without a token.

---

## `GET /api/notifications/unread-count` - signed in

What the bell asks every 30 seconds - one number, so the poll stays cheap.

**200**: `{ "count": 2 }`

**401** without a token.

---

## `POST /api/notifications/{id}/read` - signed in

Marks one of the caller's notifications read. No body.

| Outcome | Response |
| :-- | :-- |
| Theirs and unread | **204**, `readAt` set |
| Theirs and already read | **204**, nothing changes |
| Someone else's | **404** `Notification not found.` |
| No such id | **404** `Notification not found.` - the same words |
| No token | **401** |

`{id}` is constrained to a GUID (`{id:guid}`); anything else does not match the route.

---

## `POST /api/notifications/read-all` - signed in

Marks every unread notification of the caller's read. No body.

**200**: `{ "marked": 3 }` - how many were still unread. A repeat answers `{ "marked": 0 }`.

**401** without a token.

---

## Not added

- No endpoint to create, delete or mark unread a notification. Notices are created only from the bus.
- No endpoint that names a user. An administrator cannot read or mark anybody's inbox.

Later features added `GET /api/notifications/wording` and the Admin wording endpoints under the same routes
(specs/078); they are not part of this feature.
