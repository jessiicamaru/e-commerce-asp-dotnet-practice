# Contracts: Product Variants

## REST (through the gateway)

### `GET /api/products` — anonymous, unchanged shape plus two fields

`ProductResponse` gains:

| Field | Meaning |
| :-- | :-- |
| `price` | the **lowest** price among sellable variants (unchanged field, new source) |
| `priceVaries` | true when the variants do not all cost the same — the storefront shows "from" |
| `variantCount` | how many active variants |

### `GET /api/products/{id}` — anonymous

Gains `variants`: for each, `id`, `sku`, `price`, `optionSummary`, `options` (`name`/`value` pairs),
`availability` (`InStock` / `OutOfStock`), `isActive`.

### `POST /api/products` — Admin

The body gains an optional `options` (name/value pairs) for the product's **first** variant. Without
it the product gets one variant with no options, priced and SKU'd as before — the shape today.

### `POST /api/products/{id}/variants` — Admin

`{ sku, price, options: [{ name, value }] }` → 201 with the variant.

| Refusal | Status |
| :-- | :-- |
| SKU already used | 409 |
| Options identical to an existing variant of the product | 409 |
| Price ≤ 0, empty SKU, an option with no name or no value | 400 |
| Unknown product | 404 |
| Not an administrator | 401 / 403 |

### `PUT /api/products/{id}/variants/{variantId}` — Admin

`{ price, isActive }`. The SKU and the options do not change: they are what an order froze.

### `PUT /api/stock/{id}` — Admin, Inventory

`{id}` is now a **variant** id. Unchanged otherwise.

### `POST /api/cart/items` — customer

Takes `variantId` **and** keeps accepting `productId`. Exactly one must be given; a `productId` means
"the product's only variant" and is refused (409) if the product has more than one.

## gRPC

`catalog_pricing.proto` gains two methods. Nothing existing changes (research D6).

```proto
rpc PriceVariants (PriceVariantsRequest) returns (PriceVariantsResponse);
rpc DescribeVariants (DescribeVariantsRequest) returns (DescribeVariantsResponse);

message PricedVariant {
  string variant_id = 1;
  string product_id = 2;
  string sku = 3;
  string name = 4;            // the product's name
  string option_summary = 5;  // "Kit: Body only · Colour: Black"
  string price = 6;           // decimal as a string, as PricedProduct already does
  bool sellable = 7;
}
```

`PriceVariants` is all-or-nothing, like `GetPrices`: a sale is decided in full or not at all.
`DescribeVariants` answers for what exists and lists what does not, like `DescribeProducts`.

`cart_reading.proto`: `CartItem` gains `string variant_id = 3`. `product_id` stays.

## Messages

| Contract | Change |
| :-- | :-- |
| `OrderItemDto` | gains `Guid VariantId`. `ProductId` stays |
| `StockAvailabilityChangedEvent` | gains `Guid VariantId`. `ProductId` stays |

Both additive. A message from an older publisher carries `Guid.Empty`, which the consumer reads as
"the variant whose id is the product id" (research D2, D7).
