# Quickstart: Order lines fit any width

## Scenario 1 - Checkout

Put a product with a long name in the cart and open `/checkout`. Expected: the line total is whole inside "Your order"; the rule above Total is continuous.

## Scenario 2 - Phone

Open an order at 390px. Expected: name and details, then "1 × price", total at the right, nothing clipped.

## Scenario 3 - Tests

```bash
cd client && npx vitest run src/components/order && E2E_BASE_URL=http://localhost:5173 npx playwright test
```

Expected: green; with the table layout back, the flows' checkout assertion fails.
