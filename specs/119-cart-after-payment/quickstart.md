# Quickstart: The cart empties on screen when the order is paid

## Scenario 1 - In a browser

Add a product, check out. Expected: the order page turns Paid and the header's cart badge disappears without a reload.

## Scenario 2 - Tests

```bash
cd client && npx vitest run src/hooks/order && E2E_BASE_URL=http://localhost:5173 npx playwright test
```

Expected: green; with the re-read removed, the hook test and the flows' badge assertion fail.
