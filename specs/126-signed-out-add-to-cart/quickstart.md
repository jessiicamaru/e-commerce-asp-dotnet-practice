# Quickstart: A signed-out shopper is offered Add to cart

## Scenario 1 - In a browser

Signed out, open a camera with kits, choose one, press Add to cart. Expected: the sign-in page says why; after signing in, the product page with that kit chosen.

## Scenario 2 - Tests

```bash
cd client && npx vitest run src/components/product/add-to-cart src/pages/product src/pages/sign-in
```
