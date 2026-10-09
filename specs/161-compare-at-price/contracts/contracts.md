# Contracts: A compare-at price per variant

All through the gateway's `/api/products/**` route. No message, no gRPC change: `PriceVariants` (checkout's pricing)
is untouched.

## Responses

- `VariantResponse.compareAtPrice`: the compare-at in the currency asked for, or `null`.
- `ProductResponse.compareAtPrice`: the compare-at of the variant that gives the product's "from" `price`, or `null`.

## Writes - Seller (own, else 404) or Admin; never sends a product back to review

| Method | Path | Body | Answer |
| :-- | :-- | :-- | :-- |
| `PUT` | `/api/products/{id}/variants/{variantId}/prices/{currency}/compare-at` | `{ "amount": 1200000 }` | 200 `VariantResponse` in that currency. 400 not above the price, not representable in the currency, an unsupported currency, or no price in that currency |
| `DELETE` | same | - | 204 (also when there was none) |

The existing `PUT .../prices/{currency}` and `PUT .../variants/{variantId}` clear the compare-at when the new price is
not below it.

## Listing

`GET /api/products?onSale=true` - only products with an active variant with a compare-at in the currency asked for.
Combines with every other filter.
