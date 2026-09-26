# Contracts: Each seller ships their own part

> Completed on 2026-09-27, after the feature merged (#79), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. HTTP only: no message, no proto and nothing in
> `Ecommerce.Contracts` changed.

## A seller moves their part

| Method | Path | Body | Role |
| :-- | :-- | :-- | :-- |
| `POST` | `/api/orders/sales/{orderId}/preparing` | - | Seller |
| `POST` | `/api/orders/sales/{orderId}/shipment` | `{ "trackingReference": "VNPOST-123" }` | Seller |

Answer: the sale (below), as it now stands.

| Situation | Answer |
| :-- | :-- |
| No token / not a seller | 401 / 403 |
| Not their sale, no such order, not paid, failed | **404 `Sale not found.`** |
| Their part is not in the state the step starts from | 409, naming the state it is in |
| The same step again (same tracking reference) | 200, nothing changed |
| Tracking reference missing or over 100 characters | 400 |

As built (from `SellerFulfilmentCommands.cs`), the 409 wordings are:

- `Your part is already Shipped with tracking reference '{ref}'.` - the ship step again with a different
  reference;
- `Your part is {Paid|Preparing|Shipped}; only a {Paid|Preparing} part can become {Preparing|Shipped}.` -
  a skipped or backward step (a waiting part is described as `Paid`).

The 400 is `errors.TrackingReference`: `A tracking reference of at most 100 characters is required to
mark a part sent.` The request body is `ShipmentRequest { trackingReference }`, the same record the
staff route uses; the reference is trimmed.

## The shop's part - unchanged addresses

`POST /api/orders/{id}/preparing` and `POST /api/orders/{id}/shipment` (Admin) now move **the shop's
own part**. Same answers as before. An order with no shop goods: **409** "This order has no part the
shop ships; each seller ships their own."

`GET /api/orders/fulfilment?status=` lists orders by the state of **the shop's part**.

As built (`FulfilmentStep.cs`):

| Situation | Answer |
| :-- | :-- |
| No such order | 404 `Order not found.` |
| Order not paid (settling, failed) | 409 `Order is {state}; only a Paid order can be fulfilled.` |
| Order with no shop part | 409 `This order has no part the shop ships; each seller ships their own.` |
| Shop part already there with a different reference | 409 `The shop's part is already Shipped with tracking reference '{ref}'.` |
| Shop part in the wrong state | 409 `The shop's part is {state}; only a {state} part can become {to}.` |
| Repeat of the step just taken | 200, the order detail, nothing changed |

An order with no parts at all (written by an older image) is listed in the queue by `orders.Status`, as
before, until its first move creates its parts.

## A sale gains its part

```diff
 {
   "orderId": "…",
-  "status": "Paid",            // the order's
+  "status": "Paid",            // THEIR part's: Paid (waiting), Preparing, Shipped
+  "trackingReference": null,   // their part's, once shipped
+  "shippingAddress": { … },    // only while their part is Paid or Preparing
   "items": [ … ],
   …
 }
```

The list row's `status` is their part's too.

A sale with no part row of its own (an older image's order) reads the order's status mapped the same
way: `Shipped` → `Shipped`, `Preparing` → `Preparing`, anything else → waiting.

## An order gains its parts

```diff
 {
   "status": "Preparing",
   "trackingReference": null,   // the part's when there is exactly one part, else null
+  "shipments": [
+    { "status": "Shipped",   "trackingReference": "VNPOST-1", "items": ["Viltrox AF 56mm F1.4 · Mount: Sony E"] },
+    { "status": "Preparing", "trackingReference": null,       "items": ["SanDisk Extreme PRO · 64GB", "…"] }
+  ],
   …
 }
```

A part's `status` is `Paid` while waiting (it is a part of a paid order that nobody has started), so
a client needs no new word. The customer's order list row gains `shipmentCount` and
`shipmentsShipped`.

As built, each entry is `ShipmentResponse(Status, TrackingReference, Items)`, `items` being the frozen
`productName · optionSummary` of the lines in that part; the shop's part is listed first, then the
sellers' in seller-id order, so the page does not reshuffle between reads. `shipments` is omitted
(null) on the list; `shipmentCount` / `shipmentsShipped` default to `0` for an older image's order with
no parts.

## Routes, all through the gateway's existing `/api/orders/{**catch-all}` route

| Method | Path | Role | New? |
| :-- | :-- | :-- | :-- |
| `POST` | `/api/orders/sales/{id}/preparing` | `Seller` | new |
| `POST` | `/api/orders/sales/{id}/shipment` | `Seller` | new |
| `POST` | `/api/orders/{id}/preparing` | `Admin` | now the shop's part |
| `POST` | `/api/orders/{id}/shipment` | `Admin` | now the shop's part |
| `GET` | `/api/orders/fulfilment?status=` | `Admin` | now by the shop's part |
| `GET` | `/api/orders/{id}`, `/api/orders` | owner | gain `shipments`, `shipmentCount`, `shipmentsShipped` |
| `GET` | `/api/orders/sales`, `/api/orders/sales/{id}` | `Seller` | status, tracking and address from their part |
