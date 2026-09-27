# HTTP contract: A mistyped tracking reference can be corrected

Both routes go through the gateway's existing `/api/orders/**` route.

## `PUT /api/orders/sales/{orderId}/tracking`: Seller (Order)

```json
{ "trackingReference": "VN-1243" }
```

- `200`: the sale, with the part's new `trackingReference`. The same reference again is also 200, and changes nothing.
- `400` when the reference is empty or longer than 100 characters.
- `404` `Sale not found.`: no part of the caller's on this order.
- `409` when the part is not shipped, is delivered, or was cancelled (the message says which).

## `PUT /api/orders/fulfilment/{orderId}/tracking`: Admin (Order)

The same, for the shop's own part. The response is the staff order read. `409` when the order has no part of the
shop's.

## Message

`UserNotificationRequested`, kind `TrackingCorrected`, sent to the buyer with data `{ "orderId", "tracking", "shop"? }`
and link `/orders/{orderId}`. No integration event: nothing outside Order holds a tracking reference.
