# Quickstart: Product photographs fill their frame

## Scenario 1 - In a browser

Open a product with a landscape photograph. Expected: no white bands above or below it.

## Scenario 2 - Tests

```bash
cd client && npx vitest run src/components/product/product-image && E2E_BASE_URL=http://localhost:5173 npx playwright test
```
