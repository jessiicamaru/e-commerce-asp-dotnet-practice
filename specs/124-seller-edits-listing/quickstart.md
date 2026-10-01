# Quickstart: A seller edits what they listed

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter ProductDetails
cd ../client && npx vitest run src/components/seller src/pages/shop-product
```

## Scenario 2 - In a browser

As a seller, open one of your approved products: change the name - it goes back to review; add an English name - English readers see it; add a variant - it is listed with no stock.
