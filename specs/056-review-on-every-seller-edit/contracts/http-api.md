# HTTP Contract: Review on every seller edit

> Written on 2026-09-27, after the feature merged (#139), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](../spec.md)

No route, request, response or status code changed. Two existing endpoints gain a side effect: on a seller's
approved product they send it back to review. No message contract changed (the audit entries are the existing
`AuditEntryRecorded`); no gRPC contract changed.

## `POST /api/products/{id}/variants` - `Seller` or `Admin`

**Request** (unchanged): `{ "sku": "...", "price": 42000000, "options": [ { "name": "Kit", "value": "With lens" } ] }`

**Responses** (unchanged): `201` with the variant; `400` validation; `404` not yours or no such product
(`SellerOwnership`); `409` a duplicate SKU or option set.

**New side effect**: when the caller is the product's seller and the product is `Approved`, its `reviewStatus`
becomes `Pending` and it leaves the public listing and lookup until a moderator approves it again. An
administrator's call does not do this.

## `PUT /api/products/{id}/options/{optionId}/translations/{language}` - `Seller` or `Admin`

**Request** (unchanged): `{ "name": "Kit", "value": "Body only" }`

**Responses** (unchanged): `204`; `400`; `404`.

**New side effects**: records an `OptionTranslated` audit entry (before and after); and, as above, a seller's edit
of an approved product sends it back to `Pending`, recording `ProductSentForReview`.

## What the seller sees afterwards

`GET /api/products/{id}` as the seller returns the product with `reviewStatus: "Pending"`; anonymously it is a 404
(specs/045's `MaySee`). `GET /api/products` no longer lists it.
