# Data Model: A cart before signing in

## Server - no schema change

No table and no column changes. The guest cart is never stored by the server (research D1); the merge writes the
existing `cart_lines` rows of the signed-in customer's existing `carts` row.

## Browser

`localStorage["guestCart"]`:

```json
{ "lines": [ { "productId": "…", "variantId": "…", "quantity": 2 } ] }
```

- One line per variant; adding again raises its quantity, as the server's cart does (specs/020).
- At most 50 lines; a quantity from 1 to 999.
- Unreadable content is treated as an empty cart and replaced on the next write.
- Emptied after a successful merge.

## Merge rule

| Account cart | Guest cart | After the merge |
| :-- | :-- | :-- |
| none | A ×2 | A ×2 |
| A ×1 | A ×2 | A ×2 |
| A ×3 | A ×2 | A ×3 |
| C ×1 | - | C ×1 |

The same merge again: unchanged (research D2).
