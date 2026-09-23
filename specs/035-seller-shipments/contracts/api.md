# Contracts: Each seller ships their own part

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

## The shop's part - unchanged addresses

`POST /api/orders/{id}/preparing` and `POST /api/orders/{id}/shipment` (Admin) now move **the shop's
own part**. Same answers as before. An order with no shop goods: **409** "This order has no part the
shop ships; each seller ships their own."

`GET /api/orders/fulfilment?status=` lists orders by the state of **the shop's part**.

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
