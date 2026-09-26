# HTTP Contract: Shop applications

> Written on 2026-09-27, after the feature merged (#96), from the code at that merge, the pull request and
> docs/features/marketplace.md.

**Feature**: [spec.md](../spec.md)

**Service**: Identity, `http://localhost:5056`. **Through the gateway** (`:5000`): two new routes on
`identity-cluster` in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json` -
`shop-applications-route` (`/api/shop-applications/{**catch-all}`) and `shop-applications-root-route`
(`/api/shop-applications`), a pair in the shape the gateway already used for other prefixes. The record
does not say whether the root route is strictly needed beside the catch-all; it makes `POST` and `GET`
on the bare address explicit.

Controller: `ShopApplicationsController` (`[Route("api/shop-applications")]`, `[Authorize]` on the class).
Errors are RFC 7807 ProblemDetails through `Ecommerce.Shared`'s `GlobalExceptionHandler`: validation 400
with an `errors` extension, `NotFoundException` 404, `ConflictException` 409.

---

## The application record

Every endpoint below except `register-seller` answers with this shape (camelCase):

```json
{
  "id": "0199...",
  "userId": "0199...",
  "applicantEmail": "mai@example.test",
  "applicantName": "Mai Tran",
  "shopName": "Mai Lens",
  "description": "Mirrorless cameras",
  "phone": "0912 345 678",
  "status": "Pending",
  "decisionReason": null,
  "createdAt": "2026-09-23T20:00:00Z",
  "decidedAt": null
}
```

`applicantEmail` and `applicantName` are **null on the applicant's own views** (apply, `mine`) and filled
for staff (the queue, approve, reject) - FR-003, research D9. `status` is `Pending`, `Approved` or
`Rejected`.

---

## `POST /api/shop-applications` - `Customer`

A signed-in customer applies. **The applicant is the token's subject**; the body names nobody.

```json
{ "shopName": "Lan Film", "description": "Used film cameras", "phone": "0912 345 678" }
```

| Field | Rule |
| :--- | :--- |
| `shopName` | Required, not blank, at most 100 characters. Trimmed |
| `description` | Optional, at most 1000. Blank stored as null |
| `phone` | Optional, at most 20. Blank stored as null |

| Status | When |
| :--- | :--- |
| `201` | Created; the application record, own view, `status: "Pending"`. No `Location` header |
| `400` | A rule above broken |
| `401` | No or invalid token |
| `403` | The caller does not hold `Customer` |
| `404` | `User not found.` - the token names an account that no longer exists |
| `409` | `This account already sells on the shop.` / `An application is already waiting for review.` - the second also when two requests race and the partial unique index refuses one |

---

## `GET /api/shop-applications/mine` - any signed-in caller

The caller's own applications, **newest first**, own view. No id in the address; the token says whose.

`200` with an array (empty when none). `401` without a token.

---

## `GET /api/shop-applications?status=&page=&pageSize=` - `Admin`, `Moderator`

The queue, or the history.

| Query | Default | Rule |
| :--- | :--- | :--- |
| `status` | none (every status) | `Pending`, `Approved` or `Rejected`, case-insensitive |
| `page` | `1` | `> 0` |
| `pageSize` | `12` | `1` to `50` |

Order: `Pending` **oldest first**; any other value, or no filter, newest first; `id` breaks ties.

```json
{ "items": [ { "...": "staff view of the record" } ], "page": 1, "pageSize": 12, "totalCount": 3 }
```

`200`; `400` (`Status must be Pending, Approved or Rejected.`, or a paging rule); `401`; `403` for anybody
who is not staff.

---

## `POST /api/shop-applications/{id}/approve` - `Admin`, `Moderator`

No body. In one transaction with the guarded `UPDATE ... WHERE "Status" = 'Pending'`: the `Seller` role,
the shop, `SellerRegisteredEvent`, the `ShopApproved` audit entry and notice ([messages.md](./messages.md)).

| Status | When |
| :--- | :--- |
| `200` | The application record, staff view, `status: "Approved"`, `decidedAt` set |
| `401` / `403` | No token / not staff |
| `404` | `Application not found.` |
| `409` | `This application is already approved.` or `... already rejected.` - nothing was written |

---

## `POST /api/shop-applications/{id}/reject` - `Admin`, `Moderator`

```json
{ "reason": "Tell us what you sell" }
```

`reason`: required, not blank (`Reason is required.`), at most 500 characters, trimmed. The applicant
reads it on `/open-shop` and in the `ShopRejected` notice.

| Status | When |
| :--- | :--- |
| `200` | The application record, staff view, `status: "Rejected"`, `decisionReason` set |
| `400` | Reason missing, blank or too long |
| `401` / `403` | No token / not staff |
| `404` | `Application not found.` |
| `409` | Already approved or rejected - nothing was written |

---

## `POST /api/auth/register-seller` - anonymous (changed)

Same address, new meaning (research D5). The body gains two optional fields:

```json
{
  "email": "mai@example.test",
  "password": "Passw0rd!23",
  "firstName": "Mai",
  "lastName": "Tran",
  "shopName": "Mai Lens",
  "description": "Mirrorless cameras",
  "phone": "0912 345 678"
}
```

`description` (at most 1000) and `phone` (at most 20) follow the apply endpoint's rules; the rest is
unchanged, including the password rules shared with `register`.

`200` with the usual authentication response and the refresh token in the HttpOnly cookie (the body's
`refreshToken` is empty) - but **`roles` is now `["Customer"]`**, where it was `["Seller", "Customer"]`.
No shop is opened and no event is published; a `Pending` application exists instead. `409` `An account
with this email already exists.`; `400` for a broken rule.

---

## Unchanged endpoints whose answer changes

| Endpoint | Before | After |
| :--- | :--- | :--- |
| `GET /api/sellers/me` (Seller) | 200 right after `register-seller` | 403 until the application is approved and the session renewed |
| `POST /api/auth/login`, `POST /api/auth/refresh` | - | Carry `Seller` once an approval has happened |

## Authorization summary

| Endpoint | Access |
| :--- | :--- |
| `POST /api/shop-applications` | `Customer` |
| `GET /api/shop-applications/mine` | Any signed-in caller |
| `GET /api/shop-applications` | `StaffRoles.Staff` (Admin, Moderator) |
| `POST /api/shop-applications/{id}/approve`, `/reject` | `StaffRoles.Staff` |
| `POST /api/auth/register-seller` | Anonymous |
