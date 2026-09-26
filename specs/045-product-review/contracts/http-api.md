# HTTP Contract: Product review before sale

> Written on 2026-09-27, after the feature merged (#97), from the code at that merge, the pull request and docs/features/catalog.md and docs/features/moderation-and-staff.md.

**Feature**: [spec.md](../spec.md)

Everything goes through the gateway on `http://localhost:5000`. No gateway route was added:
`/api/products/{**catch-all}` (Catalog) and `/api/audit/{**catch-all}` (Activity) already cover the new
addresses. Errors are RFC 7807 ProblemDetails from `GlobalExceptionHandler`: `ValidationException` → 400
with `errors`, `NotFoundException` → 404, `ConflictException` → 409. Without a token a protected
endpoint is 401; with the wrong role, 403.

---

## Catalog - new endpoints (`ProductsController`)

### `GET /api/products/review` - Staff (Admin, Moderator)

The moderators' queue, or the history of one status.

Query (`GetReviewQueueQuery`):

| Parameter | Default | Rule |
| :-- | :-- | :-- |
| `status` | `Pending` | `Pending`, `Approved` or `Rejected`, case-insensitive; otherwise 400 `Status must be Pending, Approved or Rejected.` |
| `pageNumber` | 1 | > 0 |
| `pageSize` | 12 | 1 to 50 |

Order: `Pending` by `SubmittedAt` ascending (oldest submission first), then `Id`; `Approved` and
`Rejected` by `ReviewedAt` (else `UpdatedAt`) descending, then `Id`.

`200` - a page of product responses (the same `PaginatedList` shape as `GET /api/products`), each with
its variants, `sellerName` from Catalog's `sellers` read model (null for the shop's own) and the review
fields:

```json
{
  "items": [
    {
      "id": "…", "name": "…", "description": "…", "price": 40000000, "availability": "OutOfStock",
      "sku": "…", "categoryId": "…", "isActive": true, "imageUrl": null, "priceVaries": false,
      "variantCount": 1, "variants": [ … ], "language": "vi", "currency": "VND",
      "sellerId": "…", "sellerName": "Bruno Cameras",
      "reviewStatus": "Pending", "reviewReason": null
    }
  ],
  "pageNumber": 1, "totalPages": 1, "totalCount": 1, "hasPreviousPage": false, "hasNextPage": false
}
```

### `POST /api/products/{id}/approve` - Staff

No body. `Pending → Approved`.

- `200` - the product response, `reviewStatus: "Approved"`, `reviewReason: null`.
- `404` - no such product.
- `409` - the product is not pending: `This product is approved; nothing to do.` (the status word is the
  product's current one). Nothing is written.

### `POST /api/products/{id}/reject` - Staff

```json
{ "reason": "Photograph the actual camera" }
```

`Pending → Rejected`. `reason` is required (not empty, not whitespace) and at most 500 characters,
stored trimmed.

- `200` - the product response, `reviewStatus: "Rejected"`, `reviewReason` as sent.
- `400` - `Reason is required.` / too long.
- `404`, `409` as for approve.

### `POST /api/products/{id}/take-down` - Staff

Same body and rules as reject. `Approved → Rejected`.

- `200` - the product response, `reviewStatus: "Rejected"`, `reviewReason` as sent.
- `400`, `404`, `409` (not approved) as above.

### `POST /api/products/{id}/resubmit` - Seller, Admin

No body. `Rejected → Pending`. Ownership through `SellerOwnership.RequireCanWrite`: a seller's own
product only - somebody else's is **404**, never 403 (specs/027); an administrator passes.

- `200` - the product response, `reviewStatus: "Pending"`, `reviewReason: null`.
- `404` - missing or not yours.
- `409` - not rejected.

---

## Catalog - changed behaviour of existing endpoints

| Endpoint | Change |
| :-- | :-- |
| `POST /api/products` (Seller, Admin) | A seller's product is created `Pending`; an administrator's `Approved`. The response carries `reviewStatus` |
| `GET /api/products` (anonymous) | Lists and searches `Approved` products only |
| `GET /api/products/{id}` (anonymous) | 404 for a product that is not `Approved`, unless the caller is its seller, an Admin or a Moderator |
| `GET /api/products/mine` (Seller) | Every one of the seller's products, whatever its review state |
| `PUT /api/products/{id}/translations/{language}`, `DELETE` same | A seller's edit of an approved product returns it to `Pending`; the `PUT` response shows `reviewStatus: "Pending"` |
| `PUT`/`DELETE /api/products/{id}/image` | Same, for the product photograph |
| `PUT`/`DELETE /api/products/{id}/variants/{variantId}/image` | Same, for a variant photograph |
| Price, variant and stock endpoints | Unchanged: they never send a product back |

**Every product response** gained two fields, additive, so older clients ignore them:

| Field | Type | Meaning |
| :-- | :-- | :-- |
| `reviewStatus` | string | `Approved`, `Pending` or `Rejected`. A shopper is only ever shown approved products, so to them it always reads `Approved` |
| `reviewReason` | string or null | Why it was rejected or taken down |

Not changed at merge: `GET /api/products/{id}/image` and `GET /api/products/{id}/variants/{variantId}/image`
stayed anonymous and did not ask the review state (closed by specs/081, #166).

---

## Activity - new endpoint

### `GET /api/audit/mine` - Staff (Admin, Moderator)

`MyDecisionsController`. The caller's own Moderation audit entries, newest first - the dashboard's
"recently". The actor is the token's user id; nothing in the request names it.

Query: `page` (default 1, at least 1), `pageSize` (default 12, 1 to 100 - the validator of the
underlying `GetAuditEntriesQuery`).

`200`:

```json
{
  "items": [
    {
      "id": "…", "category": "Moderation", "action": "ProductApproved",
      "actorId": "…", "actorEmail": "admin@…", "actorRole": "Admin",
      "subjectType": "Product", "subjectId": "…",
      "summary": "\"Bruno seller camera\" approved", "service": "catalog",
      "occurredAt": "2026-09-24T…Z", "changeCount": …
    }
  ],
  "page": 1, "pageSize": 12, "totalCount": 1
}
```

It includes shop decisions from specs/044 and account moderation from specs/043 - every Moderation entry
the caller is the actor of - and nothing else of the audit log, which stays Admin-only at `/api/audit`.

---

## Authorization summary

| Endpoint | Access |
| :-- | :-- |
| `GET /api/products/review` | Admin, Moderator |
| `POST /api/products/{id}/approve`, `/reject`, `/take-down` | Admin, Moderator |
| `POST /api/products/{id}/resubmit` | Seller (own product), Admin |
| `GET /api/audit/mine` | Admin, Moderator |
| `GET /api/products`, `GET /api/products/{id}` | Anonymous; hidden products only to their seller and staff |
