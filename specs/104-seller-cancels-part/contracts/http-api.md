# HTTP contract: A seller cancels the part of an order they cannot fulfil

Both routes go through the gateway's existing `/api/orders/**` route.

## `POST /api/orders/sales/{orderId}/cancel`: Seller (Order)

```json
{ "reason": "Out of stock - the last one was damaged." }
```

- `200`: the sale, with the part now carrying `cancelledAt`, `cancelReason` and `cancelledBy: "Seller"`. A part
  already cancelled is also 200 and changes nothing.
- `400` when `reason` is empty or longer than 500 characters.
- `404` `Sale not found.` for an order with no part of the caller's, the same answer as for any sale that is not
  theirs.
- `409` when the part has shipped, or the order is not paid (failed, still settling, or already cancelled).

## `POST /api/orders/fulfilment/{orderId}/shop-part/cancel`: Admin (Order)

The same body and rules, for the part with no seller. The response is the staff order read. `409` when the order has
no part of the shop's own.

## Read shapes

Every part in `GET /api/orders/{id}` (buyer), `GET /api/orders/sales/{id}` (seller) and
`GET /api/orders/fulfilment/{id}` (staff) gains:

```json
{ "cancelledAt": "2026-09-27T09:10:00Z", "cancelReason": "Out of stock", "cancelledBy": "Seller" }
```

All three are null for a part that is not cancelled.
