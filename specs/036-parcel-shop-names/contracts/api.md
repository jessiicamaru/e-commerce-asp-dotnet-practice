# Contracts: Which shop each parcel comes from

> Completed on 2026-09-27, after the feature merged (#80), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. One proto field and additive HTTP fields; no message changed.

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

## As built (from the code at #80)

**gRPC.** `seller_name` is set on both `PriceVariants` and `DescribeVariants` answers of
`CatalogPricing`, only when the product has a seller **and** Catalog's `sellers` read model has a name for
them. Order reads it with `HasSellerName` and treats blank as absent (`GrpcCatalogPrices.SellerNameOf`).
Field 10 is new; no number was reused.

**HTTP**, all under the existing `/api/orders` gateway route, no new endpoint:

| Response | Field | Values |
| :-- | :-- | :-- |
| `OrderItemDetailResponse` (lines of `GET /api/orders/{id}`) and `OrderItemResponse` (lines of the checkout response) | `sellerName` | a name or `null` |
| quote `items[]` (`GET /api/orders/quote`, the same `OrderItemResponse`) | `sellerName` | a name or `null`, from the live answer |
| `ShipmentResponse` (`shipments[]` on `GET /api/orders/{id}`) | `sellerName` | the name frozen on the part's lines, or `null` |
| `ShipmentResponse` | `isShop` | `true` for the shop's own part (`SellerId` null), else `false` |

Status codes are unchanged. A customer is told a seller's **name** and nothing else about them (FR-005).

## Messages

None changed. The name comes from Catalog's read model, which `SellerRegisteredEvent` and
`SellerRenamedEvent` (specs/027) already feed.
