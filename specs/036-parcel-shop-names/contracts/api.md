# Contracts: Which shop each parcel comes from

## gRPC - additive

```diff
 message PricedVariant {
   …
   optional string seller_id = 9;
+  // The selling shop's name as Catalog knows it now (specs/036). Unset for the shop's own goods and
+  // when the name is not known yet. Order freezes it onto the line.
+  optional string seller_name = 10;
 }
```

## HTTP - additive fields

- Order lines (`GET /api/orders/{id}`, the checkout response, `GET /api/orders/quote`):
  `"sellerName": "Mai Lens Hà Nội"` or `null`.
- Parcels (`shipments[]` on an order): `"sellerName"` and `"isShop"`.

Nothing is removed or renamed.
