# HTTP Contract: A Cart and an Address Book

> Written on 2026-09-27, after the feature merged (#47), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md and docs/architecture/storefront.md.

**Feature**: [spec.md](../spec.md)

**No API changed** - "every endpoint #37 needs already existed (specs/010, specs/011)". Recorded here as
the storefront relies on them at the merge. All through the gateway at `http://localhost:5000`, all
`[Authorize]` (a bearer token; the user comes from it, never from the request). Errors are RFC 7807
ProblemDetails.

---

## Cart (`CartController`, Cart service `:5062`)

| Method and path | Body | Success | Refusals |
| :--- | :--- | :--- | :--- |
| `GET /api/cart` | - | `200` `{ lines, estimatedTotal, canCheckOut, pricesAvailable }` | 401 |
| `POST /api/cart/items` | `{ "productId", "quantity" }` | `204`; adds to an existing line | 400 when `quantity` ≤ 0 ("Quantity must be greater than 0.") |
| `PUT /api/cart/items/{productId}` | `{ "quantity" }` | `204`; 0 removes the line | 400 when negative ("Quantity cannot be negative.") |
| `DELETE /api/cart/items/{productId}` | - | `204` | - |
| `DELETE /api/cart` | - | `204` | - (not called by any page yet) |

A line: `{ "productId", "name", "quantity", "unitPrice", "lineTotal", "status" }`, where `name`,
`unitPrice` and `lineTotal` are Catalog's **today**, and null when Catalog could not answer
(`pricesAvailable: false`). `status` is `Available`, `NotForSale`, `NoLongerAvailable` or
`PriceUnavailable`.

## Addresses (`AddressesController`, Identity `:5056`)

| Method and path | Body | Success | Refusals |
| :--- | :--- | :--- | :--- |
| `GET /api/addresses` | - | `200` array of addresses | 401 |
| `POST /api/addresses` | address fields | `201 Created` with the address | 400 with `errors` per field |
| `PUT /api/addresses/{id}` | address fields | `200` with the address | 400; 404 for an address that is not the caller's |
| `DELETE /api/addresses/{id}` | - | `204` | 404 as above |
| `PUT /api/addresses/{id}/default` | - | `204` | 404 as above |

Address fields: `recipientName` (required, ≤ 100), `line1` (required, ≤ 200), `line2` (≤ 200), `city`
(required, ≤ 100), `region` (≤ 100), `postalCode`, `country` (two letters), `phone`. The first address
saved becomes the default; making another default clears the old one.

The pull request's run: `postal "!"` and `country "ZZ"` → 400 with `errors.PostalCode` and
`errors.Country`.
