# Contract: The Cart Service

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-21

| | REST | gRPC |
| :-- | :-- | :-- |
| host port | **5062** | **6062** |
| container port | 8080 | 8081 |
| caller | the customer | Order, at checkout |
| database | `ecommerce_cart_db` on host **5439** | |

Every endpoint requires a validated token. **No endpoint accepts a user id** — the cart is always the
caller's own, read from the token (Principle IV).

---

## REST — the customer's cart

| Method | Path | Body | Effect |
| :-- | :-- | :-- | :-- |
| `GET` | `/api/cart` | — | the caller's cart, priced from Catalog |
| `POST` | `/api/cart/items` | `{ productId, quantity }` | add, or raise the quantity if already present |
| `PUT` | `/api/cart/items/{productId}` | `{ quantity }` | set the quantity; `0` removes the line |
| `DELETE` | `/api/cart/items/{productId}` | — | remove the line |
| `DELETE` | `/api/cart` | — | empty the cart |

A customer with no cart yet reads an empty one — not a 404. A cart is created on the first add.

### The read

```json
{
  "lines": [
    { "productId": "…", "name": "E2E Widget", "quantity": 3,
      "unitPrice": 9.99, "lineTotal": 29.97, "status": "Available" },
    { "productId": "…", "name": null, "quantity": 1,
      "unitPrice": null, "lineTotal": null, "status": "NoLongerAvailable" }
  ],
  "estimatedTotal": 29.97,
  "canCheckOut": false,
  "pricesAvailable": true
}
```

- `status` is `Available`, `NotForSale`, `NoLongerAvailable`, or `PriceUnavailable`.
- **`estimatedTotal`**, not `total`, deliberately. The charge is decided at checkout.
- `canCheckOut` is false while any line is `NotForSale` or `NoLongerAvailable`, so a client can say
  why before the customer tries.
- `pricesAvailable` is false when Catalog could not be reached; the lines are still returned.

---

## gRPC — Order reading the caller's cart

```proto
service CartReading {
  rpc GetMyCart (GetMyCartRequest) returns (GetMyCartResponse);
}
message GetMyCartRequest {}             // deliberately empty: WHOSE cart comes from the token
message GetMyCartResponse {
  repeated CartItem items = 1;
}
message CartItem {
  string product_id = 1;
  int32  quantity   = 2;
}
```

**The request is empty on purpose.** Order forwards the customer's bearer token in the
`authorization` metadata, and Cart reads the user from it through `ICurrentUser` exactly as its REST
endpoints do. A `user_id` field would let anything on the network read anybody's cart by naming them
— the `UserId`-in-the-body defect again, one hop further in ([research D4](../research.md)).

**No price in the response.** Checkout re-prices from Catalog, which owns the price.

| Status | Meaning |
| :-- | :-- |
| `OK` | the cart, possibly empty |
| `UNAUTHENTICATED` | no or invalid token forwarded |

An empty cart is `OK` with no items; Order refuses the checkout, not Cart.

---

## Catalog — one additive RPC

`CatalogPricing` gains `DescribeProducts` beside the existing `GetPrices`
([research D8](../research.md)):

```proto
rpc DescribeProducts (DescribeProductsRequest) returns (DescribeProductsResponse);

message DescribeProductsRequest  { repeated string product_ids = 1; }
message DescribeProductsResponse {
  repeated PricedProduct products = 1;              // the ones that exist
  repeated string        missing_product_ids = 2;   // the ones that do not
}
```

**Never `NOT_FOUND`.** `GetPrices` refuses everything if one product is missing — correct for
checkout. `DescribeProducts` answers the rest and lists what it could not find — correct for display.
Additive: no existing method or message changes.

---

## Events Cart consumes

| Event | Queue (prefixed, [research D7](../research.md)) |
| :-- | :-- |
| `OrderSubmittedEvent` | `CartSvcOrderSubmitted` |
| `OrderCompletedEvent` | `CartSvcOrderCompleted` |
| `OrderFailedEvent` | `CartSvcOrderFailed` |

Cart publishes nothing.
