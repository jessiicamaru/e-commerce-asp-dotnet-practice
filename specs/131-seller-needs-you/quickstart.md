# Quickstart: A seller's home says what needs them

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter SalesFilter
cd ../client && npx vitest run src/pages/shop src/layouts/seller-layout
```

## Scenario 2 - In a browser

As a seller with a new paid sale: `/shop` says one sale to prepare, Sales has a badge 1; Start preparing - both go.
