# Contracts: A seller can see what they sold

## Two reads, sellers only

| Method | Path | Answers |
| :-- | :-- | :-- |
| `GET` | `/api/orders/sales?page=1&pageSize=20` | A page of the caller's sales, newest first |
| `GET` | `/api/orders/sales/{orderId}` | One sale, the caller's lines only |

`[Authorize(Roles = "Seller")]` on both. No seller id in the path, the query or anything else: the
seller is the token's subject (Constitution IV). Both go through the existing gateway route
`/api/orders/{**catch-all}`.

### The list

```json
{
  "items": [
    {
      "orderId": "0199…",
      "status": "Paid",
      "createdAt": "2026-09-23T08:14:02Z",
      "updatedAt": "2026-09-23T08:14:04Z",
      "lineCount": 1,
      "units": 2,
      "subtotal": 104000000,
      "currency": "VND"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

`subtotal` is `Σ unitPrice × quantity` over **the caller's lines**, before tax. `status` is one of
`Paid`, `Preparing`, `Shipped` - a legacy `Completed` reads as `Paid`.

`page ≥ 1`, `1 ≤ pageSize ≤ 100`; otherwise `400`.

### One sale

```json
{
  "orderId": "0199…",
  "status": "Shipped",
  "createdAt": "2026-09-23T08:14:02Z",
  "updatedAt": "2026-09-23T09:30:11Z",
  "items": [
    {
      "productId": "…", "productName": "Sony A7 IV", "quantity": 2,
      "unitPrice": 52000000, "totalPrice": 104000000, "taxAmount": 10400000,
      "variantId": "…", "sku": "SONY-A7M4", "optionSummary": "Kit: Body only"
    }
  ],
  "subtotal": 104000000,
  "currency": "VND",
  "language": "vi"
}
```

⚠️ **Deliberately absent** (research D4), and a test fails if one appears: `userId`,
`shippingAddress`, `totalAmount`, `shippingPrice`, `taxTotal`, `trackingReference`, and any line that
is not the caller's.

### Refusals

| Situation | Answer |
| :-- | :-- |
| No token | `401` |
| Signed in, not a seller | `403` |
| No such order / holds none of the caller's lines / `Failed` / `Submitted` | `404 "Sale not found."` - one wording for all four |

## The pricing answer gains a field

```diff
 message PricedVariant {
   …
   string currency = 8;
+
+  // Whose product this is (specs/034), frozen onto the order line. Empty means the shop's own.
+  // `optional` so an older Catalog, which sends nothing, is distinguishable from "the shop's":
+  // HasSellerId is false then, and Order records no seller and logs a warning.
+  optional string seller_id = 9;
 }
```

Additive. `DescribeVariants` carries it too, because both answers are built by one method; nothing
reads it there.
