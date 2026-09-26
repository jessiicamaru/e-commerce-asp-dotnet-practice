# HTTP Contract: A Total With Something Behind It

> Written on 2026-09-27, after the feature merged (#32), from the code at that merge, the pull request
> and docs/features/shopping-and-checkout.md.

**Feature**: [spec.md](../spec.md)

No endpoint was added, removed or re-authorised. Three existing Order responses gained fields; every
new field is **additive and nullable**, so a client that ignores them is unaffected. No request changed
at all - the feature takes nothing new from the client (plan, principle IV).

**Base**: Order on `http://localhost:5059`, through the gateway at `http://localhost:5000/api/orders`.
Errors are RFC 7807 ProblemDetails from the shared `GlobalExceptionHandler`.

---

## `POST /api/orders` - signed in

Request unchanged: `{ "addressId": "<guid or null>", "shippingOption": "standard" | "express" }`.

Response `200`, `OrderResponse`, with the parts added at the end (values from the pull request's run:
3 × 9.99 express to VN at 10%):

```json
{
  "orderId": "0199...",
  "userId": "0199...",
  "totalAmount": 49.47,
  "status": "Submitted",
  "createdAt": "...",
  "items": [
    { "productId": "...", "productName": "Widget", "quantity": 3,
      "unitPrice": 9.99, "totalPrice": 29.97, "taxAmount": 3.00 }
  ],
  "shippingAddress": { "recipientName": "...", "country": "VN", "...": "..." },
  "shippingOption": { "code": "express", "name": "Express delivery" },
  "shippingPrice": 15.00,
  "subtotal": 29.97,
  "taxTotal": 4.50,
  "discountTotal": 0,
  "taxRate": 0.1
}
```

`subtotal + shippingPrice + taxTotal - discountTotal = totalAmount` always holds for an order placed by
this feature: 29.97 + 15.00 + 4.50 − 0 = 49.47 (3.00 on the line, 1.50 on delivery).

The refusals are unchanged from features 009-011 (400, 404, 409, 503). The tax adds no new refusal: a
bad rate stops the service at startup rather than answering a request.

## `GET /api/orders/{id}` - signed in, own orders only

`OrderDetailResponse` gains `subtotal`, `taxTotal`, `discountTotal`, `taxRate` (after the existing
`trackingReference`), and each item in `items` gains `taxAmount`. For an order placed before this
feature they read as backfilled: `subtotal` = total less delivery, `taxTotal` 0, `discountTotal` 0,
`taxRate` 0, `taxAmount` 0. Another customer's order is still a 404.

## `GET /api/orders` - signed in

Unchanged. `OrderSummaryResponse` carries `totalAmount` only; the parts are on the detail.

---

## Which response carries what

| Field | `POST /api/orders` | `GET /api/orders/{id}` | Null when |
| :--- | :--- | :--- | :--- |
| `subtotal`, `taxTotal`, `discountTotal`, `taxRate` | yes | yes | an Order image from before this feature wrote the row after the migration ran (rollback) |
| `items[].taxAmount` | yes | yes | as above |
| `shippingPrice` | yes (feature 011) | yes (feature 011) | an order from before feature 011 |

The C# records: `OrderResponse` / `OrderItemResponse` in
`Orders/Commands/SubmitOrder/SubmitOrderCommand.cs`, `OrderDetailResponse` / `OrderItemDetailResponse`
in `Orders/Common/OrderResponses.cs`, all with the new members as optional (`= null`) positional
parameters.
