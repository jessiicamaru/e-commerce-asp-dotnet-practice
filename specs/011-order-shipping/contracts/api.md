# Contracts: Somewhere for the Order to Go

All REST paths are as seen through the gateway (`:5000`). Errors are RFC 7807 ProblemDetails.

---

## Identity — address book (`/api/addresses`, `[Authorize]`)

The customer is **always** the token's subject. No route or body field names a user; one that is
sent is ignored.

| Method | Path | Body | Success | Errors |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/addresses` | — | `200` list, default first | `401` |
| `GET` | `/api/addresses/{id}` | — | `200` | `404` — also for another customer's id |
| `POST` | `/api/addresses` | Address | `201` + `Location` | `400` (field errors), `409` (20 already) |
| `PUT` | `/api/addresses/{id}` | Address | `200` | `400`, `404` |
| `DELETE` | `/api/addresses/{id}` | — | `204` | `404` |
| `PUT` | `/api/addresses/{id}/default` | — | `204` | `404` |

**Address body**

```json
{
  "recipientName": "Nguyen Van A",
  "line1": "12 Ly Thuong Kiet",
  "line2": null,
  "city": "Ha Noi",
  "region": null,
  "postalCode": "100000",
  "country": "VN",
  "phone": "+84 912 345 678"
}
```

**Response** adds `id`, `isDefault`. A 400 lists each failing field; its wording says a field is
*not well-formed*, never that an address is *invalid* or *undeliverable*.

---

## Identity — gRPC `AddressReading` (Identity `:6056` host / `8081` container)

```proto
service AddressReading {
  // The caller is identified from the forwarded bearer token. address_id "" means "my default".
  rpc GetMyAddress (GetMyAddressRequest) returns (GetMyAddressResponse);
}
message GetMyAddressRequest  { string address_id = 1; }
message GetMyAddressResponse { bool found = 1; DeliveryAddress address = 2; }
message DeliveryAddress {
  string id = 1; string recipient_name = 2; string line1 = 3; string line2 = 4;
  string city = 5; string region = 6; string postal_code = 7; string country = 8; string phone = 9;
}
```

- No token → `UNAUTHENTICATED`.
- Not found, not the caller's, or no default → `found = false`, indistinguishably.
- The request has **no user field**, and never will.

---

## Order — checkout (changed)

`POST /api/orders` — `[Authorize]`

```json
{ "addressId": "0199…", "shippingOption": "express" }
```

| Field | Required | Meaning |
| :--- | :--- | :--- |
| `shippingOption` | **yes** | a code from `GET /api/orders/shipping-options` |
| `addressId` | no | omitted or null → the customer's default |

Items, prices and the user are **not** fields; anything else sent is ignored.

| Outcome | Status |
| :--- | :--- |
| accepted | `200` — the order, `Submitted`, with `shippingAddress`, `shippingOption`, `shippingPrice`, `totalAmount` (= items + shipping) |
| `shippingOption` missing or unknown | `400` |
| `addressId` not found (or not the caller's) | `404` |
| no `addressId` and no default | `409` |
| cart empty, or an item not for sale | `409` (unchanged) |
| Cart, Catalog or Identity unreachable | `503` |

**Breaking**: a body-less `POST /api/orders` was valid since feature 010 and is now `400`.

## Order — delivery options (new)

`GET /api/orders/shipping-options` — `[AllowAnonymous]`

```json
[ { "code": "standard", "name": "Standard delivery", "price": 5.00 },
  { "code": "express",  "name": "Express delivery",  "price": 15.00 } ]
```

## Order — reading an order (changed shape, same routes)

`GET /api/orders/{id}` and `GET /api/orders` gain:

```json
"status": "Paid",                 // was "Completed"; also Preparing, Shipped
"shippingAddress": { … } | null,  // null for orders placed before this feature
"shippingOption": { "code": "express", "name": "Express delivery" } | null,
"shippingPrice": 15.00 | null,
"trackingReference": "VN123456789" | null
```

## Order — fulfilment (new, `[Authorize(Roles = "Admin")]`)

| Method | Path | Body | Success | Errors |
| :--- | :--- | :--- | :--- | :--- |
| `GET` | `/api/orders/fulfilment?status=Paid&page=1&pageSize=20` | — | `200` paged, any customer's | `400` unknown status |
| `POST` | `/api/orders/{id}/preparing` | — | `200` the order | `404`, `409` not `Paid` |
| `POST` | `/api/orders/{id}/shipment` | `{ "trackingReference": "VN123" }` | `200` the order | `400` empty reference, `404`, `409` not `Preparing` |

**Repeats** (FR-016): `preparing` on an order already `Preparing` → `200`, unchanged. `shipment` on
an order already `Shipped` with the **same** reference → `200`, unchanged; with a different one →
`409`. Customers → `403`.

---

## Messages

**None change.** `OrderSubmittedEvent.TotalAmount` now includes shipping, so the saga's
`ProcessPaymentCommand.Amount` does too — same fields, same types, a different sum.
