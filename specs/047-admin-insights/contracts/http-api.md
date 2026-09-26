# HTTP Contract: Admin insights

> Written on 2026-09-27, after the feature merged (#99, #101), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](../spec.md) | **Decisions**: [research.md](../research.md)

Seven new endpoints in three services, all through the gateway on `http://localhost:5000`. No gateway change was
needed: `order-route` (`/api/orders/{**catch-all}`), `catalog-products-route` (`/api/products/{**catch-all}`) and the
`users-route` (`/api/users/{**catch-all}`) already covered every path.

**This feature has no messages and no gRPC.** Nothing is published or consumed, and no proto changed: the
insights are reads, and the view counter is one SQL statement (research D1, D3). So there is no `messages.md` or
`grpc.md`.

Errors are RFC 7807 ProblemDetails through `Ecommerce.Shared`'s `GlobalExceptionHandler`: a validation failure is
400 with an `errors` extension; no token is 401; a token without the `Admin` role is 403.

JSON is camelCase; a `DateOnly` is `"YYYY-MM-DD"`; an instant is ISO 8601 UTC.

---

## The period (Order and Catalog)

`from` and `to` are optional query parameters on every insight.

| Service | Default | Bounds | Refused (400) |
| :-- | :-- | :-- | :-- |
| Order (`revenue`, `top-products`, `top-buyers`) | `to` = now, `from` = `to` - 30 days | `CreatedAt >= from AND CreatedAt < to`, as UTC instants | `from` not before `to` (both given): "The period must start before it ends."; revenue only: more than 366 days, "The period can be at most 366 days." |
| Catalog (`top-viewed`) | `to` = now, `from` = `to` - 30 days | the UTC dates of `from` and `to`, both days included | `from` not before `to` (both given) |

At the merge the two rules differ and only revenue is capped; specs/055 made one rule for all four (plan,
"What this feature does not finish").

---

## `GET /api/orders/insights/revenue` - Admin

`InsightsController` (`[Route("api/orders/insights")]`, `[Authorize(Roles = "Admin")]`) → `GetRevenueQuery`.

**Query**: `from`, `to`.

**200**:

```json
{
  "from": "2026-08-25T10:00:00Z",
  "to": "2026-09-24T10:00:00Z",
  "totals": [
    { "currency": "VND", "revenue": 84000000, "orders": 2, "averageOrderValue": 42000000 },
    { "currency": "USD", "revenue": 1700.00, "orders": 1, "averageOrderValue": 1700.00 }
  ],
  "days": [
    { "day": "2026-09-20", "currency": "VND", "revenue": 84000000, "orders": 2 },
    { "day": "2026-09-21", "currency": "USD", "revenue": 1700.00, "orders": 1 }
  ]
}
```

- `from` / `to` are the resolved period.
- `totals`: one per currency, highest revenue first. `revenue` sums `TotalAmount` of the sales (research D5, D8);
  `averageOrderValue` = revenue / orders, rounded to 2 places, halves away from zero.
- `days`: one per currency per UTC day **that had sales** - empty days are not in the response; the storefront
  fills them in (#101). Ordered by day, then currency.
- An order with no currency counts under the default currency.

**400**: the period rules above. **401**, **403** as usual.

---

## `GET /api/orders/insights/top-products` - Admin

→ `GetTopProductsQuery`.

**Query**: `from`, `to`; `by` = `units` (default) or `revenue`; `currency` (default: the default currency; used
only by `by=revenue`); `limit` 1-50, default 10.

**200**:

```json
[
  {
    "productId": "0199....",
    "productName": "Fujifilm X-T5",
    "units": 4,
    "revenue": [ { "currency": "VND", "amount": 3000 }, { "currency": "USD", "amount": 10.00 } ]
  }
]
```

- `units` sums `Quantity` across currencies. `revenue` is per currency, `Quantity * UnitPrice` (goods before
  tax), largest first.
- `by=units`: most units first, ties by name. `by=revenue`: most revenue in `currency` first (none in it = 0),
  ties by units.
- `productName` is a name frozen on an order line - within a currency, the line of the most recent order.

**400**: `by` not `units`/`revenue` ("By must be units or revenue."), `limit` outside 1-50, `from` not before
`to`.

---

## `GET /api/orders/insights/top-buyers` - Admin

→ `GetTopBuyersQuery`.

**Query**: `from`, `to`, `currency` (default: the default currency), `limit` 1-50, default 10.

**200**:

```json
[
  { "customerId": "0199....", "orders": 2, "spent": [ { "currency": "VND", "amount": 84000000 } ] }
]
```

Ranked by `spent` in `currency` (none in it = 0), ties by `orders`. Ids only: the email is Identity's
(`/api/users/lookup` below).

**400**: `limit` outside 1-50, `from` not before `to`.

---

## `POST /api/products/{id}/view` - anonymous

`ProductsController.View`, `[AllowAnonymous]` → `RecordProductViewCommand(id)`. No body.

**204**, always - counted or not. Counted (one `product_views` increment for today, UTC) only when the product
exists, is listed (`IsListed`: its review status is `Approved`), and the caller is not `Admin`, not `Moderator` and not the
product's seller. An anonymous caller counts. A token is optional; when present, roles and id come from it.

An `id` that is not a GUID does not match the route (`{id:guid}`) and is a 404 from routing, before the handler.

---

## `GET /api/products/insights/top-viewed` - Admin

`ProductsController.TopViewed`, `[Authorize(Roles = "Admin")]` → `GetTopViewedQuery`.

**Query**: `from`, `to` (days, both included), `limit` 1-50, default 10.

**200**:

```json
[ { "productId": "0199....", "name": "Sony A7 IV", "views": 41 } ]
```

Most views first. `name` is Catalog's current default-language name.

**400**: `limit` outside 1-50, `from` not before `to`.

---

## `GET /api/users/lookup?ids=...` - Admin

`UsersController.Lookup`, `[Authorize(Roles = "Admin")]` → `LookupUsersQuery`. `ids` is repeated:
`?ids=<guid>&ids=<guid>`.

**200**:

```json
[ { "id": "0199....", "email": "lan@example.test", "firstName": "Lan", "lastName": "Pham" } ]
```

Unknown ids are left out; duplicates are asked once.

**400**: no ids, or more than 100 ("Between 1 and 100 ids."). A value that is not a GUID fails model binding.

---

## `GET /api/users/stats` - Admin

`UsersController.Stats`, `[Authorize(Roles = "Admin")]` → `GetUserStatsQuery`.

**200**:

```json
{ "total": 20, "customers": 18, "sellers": 3, "moderators": 1, "admins": 1, "locked": 1, "banned": 1 }
```

`customers`, `sellers`, `moderators`, `admins` count role memberships (a seller also holds `Customer`); `locked`
counts `LockedUntil` in the future; `banned` counts `BannedAt` set. Counts are for now, not for a period.

---

## Existing endpoints the Overview also reads

Unchanged, and callable by staff (Admin or Moderator):

| Request | For |
| :-- | :-- |
| `GET /api/products/review?status=Pending&pageNumber=1&pageSize=1` (specs/045) | "Products to review": its `totalCount` |
| `GET /api/shop-applications?status=Pending&page=1&pageSize=1` (specs/044) | "Shop applications": its `totalCount` |

---

## Authorization

| Endpoint | Access |
| :-- | :-- |
| `GET /api/orders/insights/revenue`, `/top-products`, `/top-buyers` | `Admin` |
| `GET /api/products/insights/top-viewed` | `Admin` |
| `GET /api/users/lookup`, `GET /api/users/stats` | `Admin` |
| `POST /api/products/{id}/view` | Anonymous (explicit `[AllowAnonymous]`) |

A moderator is refused like a customer (research D11).
