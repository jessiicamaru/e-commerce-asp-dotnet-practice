# Quickstart: A cart before signing in

Against the compose stack rebuilt from this branch.

## 1. Price lines anonymously

```bash
curl -s -X POST localhost:5000/api/cart/price -H 'Content-Type: application/json' -H 'X-Currency: VND' \
  -d '{"lines":[{"productId":"<P>","variantId":"<V>","quantity":2}]}' | jq '.lines[0].status, .estimatedTotal'
```

Expected: `"Available"` and twice the variant's price. No row is written to `ecommerce_cart_db`.

## 2. Merge

Signed in as a customer whose cart holds `<V>` ×1, post the same body to `/api/cart/merge` (204), then `GET /api/cart`:
`<V>` ×2. Post it again: still ×2.

## 3. Refusals

51 lines, or a quantity of 0 or 1000: 400 on both.

## 4. The shop

Signed out at `localhost:8088`: add two products; the header counts them; reload; `/cart` shows them priced; change and
remove; "Checkout" offers sign-in and returns to the cart, where the lines are now the account's and the browser's
cart is empty.
