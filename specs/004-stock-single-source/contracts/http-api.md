# HTTP Contracts: One Source of Truth for Stock

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-17

Base: `http://localhost:5057/api/products`, or `http://localhost:5000/api/products` through the
gateway.

> [!CAUTION]
> **Breaking.** `stockQuantity` leaves both the product payload and the create-product request. This
> is deliberate and has no deprecation period — see research D5. There is no frontend in this
> repository and no external consumer, so the blast radius is the API surface and the tests that
> touch it. A caller that breaks finds out immediately; a caller reading a stale number never does.

---

## `GET /api/products` — changed

Still `[AllowAnonymous]`. Still the same query parameters.

### Before

```json
{
  "id": "01a0ad7d-...",
  "name": "Mechanical Keyboard",
  "price": 129.99,
  "stockQuantity": 50,
  "sku": "KEY-001",
  "categoryId": "01a0ad7c-...",
  "isActive": true
}
```

`stockQuantity` was whatever was typed at creation. In the feature 003 run it read `50` while the
real figure was `8`.

### After

```json
{
  "id": "01a0ad7d-...",
  "name": "Mechanical Keyboard",
  "price": 129.99,
  "availability": "InStock",
  "sku": "KEY-001",
  "categoryId": "01a0ad7c-...",
  "isActive": true
}
```

| Value | Meaning |
| :--- | :--- |
| `"InStock"` | Inventory last said units are available to a new shopper |
| `"OutOfStock"` | Inventory last said none are — **or has never said anything** |

**A string, not a boolean.** `"availability": "InStock"` leaves room for a third value later
(a low-stock state, say) without changing the field's type, and it reads correctly in a payload
where `true` would have to be mentally attached to a field name to mean anything. It follows the
project's existing convention for enums on the wire — `status` on an order is `"Completed"`, not a
number.

**No stock count is exposed by this service, at any level of detail.** That is the feature.

### What it does not tell you

`"InStock"` is what Inventory last announced, not a reservation. It can be up to 10 seconds stale
(FR-010), and a product can read `"InStock"` while another shopper is reserving its last unit.
Checkout is where availability is decided, under a row lock, against Inventory — not here.

---

## `GET /api/products/{id}` — changed

Same substitution, same rules.

---

## `POST /api/products` — changed

Still `[Authorize(Roles = "Admin")]`.

### Before

```json
{
  "name": "Mechanical Keyboard",
  "description": "Blue switches",
  "price": 129.99,
  "stockQuantity": 50,
  "sku": "KEY-001",
  "categoryId": "01a0ad7c-..."
}
```

### After

```json
{
  "name": "Mechanical Keyboard",
  "description": "Blue switches",
  "price": 129.99,
  "sku": "KEY-001",
  "categoryId": "01a0ad7c-..."
}
```

`stockQuantity` is **rejected**, not ignored: an unknown field must not be silently accepted, or an
administrator will believe they set stock when they did not. The validator rule that required it to
be positive goes with it.

The response carries `"availability": "OutOfStock"`, which is true — nothing has been stocked yet.

### Setting stock is now a second, explicit step

```bash
PUT http://localhost:5060/api/stock/{productId}    # Admin
{ "quantityOnHand": 50 }
```

This was always the only call that actually did anything. It is now visibly required rather than
silently optional — the rejected alternative was carrying an opening quantity on
`ProductCreatedEvent`, which would have meant the catalogue telling the stock owner what its stock
is.

---

## Unchanged

- `GET /api/stock` and `GET /api/stock/{productId}` on Inventory (5060). Both already
  `[AllowAnonymous]`, both already report `quantityOnHand`, `quantityReserved` and
  `quantityAvailable`. **This is where a real number comes from, and it did not move.**
- `POST /api/categories`, `GET /api/categories`.
- Every order, payment and inventory endpoint.

---

## Gateway

Covered by the existing `catalog-route`. No new route or cluster — this feature changes the shape of
payloads on paths that already exist.
