# Contracts: A cart before signing in

Through the gateway's existing `/api/cart/**` route. No message, no gRPC change.

## `POST /api/cart/price` - anonymous

```json
{ "lines": [ { "productId": "…", "variantId": "…", "quantity": 2 } ] }
```

200 `CartResponse` - the same shape as `GET /api/cart`: lines with name, option summary, unit price, line total and
status, the estimate, `canCheckOut`, `pricesAvailable`, `currency` - in the language and currency of the request.
Stores nothing. 400: more than 50 lines, a quantity outside 1-999, an empty product id.

## `POST /api/cart/merge` - signed in

Same body. 204. Each line is added to the caller's cart when absent, or raised to the given quantity when the cart holds
fewer (never lowered). One transaction under the cart's lock. Repeating it changes nothing. 400 as above.

## Unchanged

`GET /api/cart`, `POST /api/cart/items`, `PUT`/`DELETE /api/cart/items/{id}`, `DELETE /api/cart` - signed in, as before.
