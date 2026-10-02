# Quickstart: Orders are recognisable

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter OrderPreview
cd ../client && npx vitest run src/components/order src/pages/orders src/pages/shop-sales
```

## Scenario 2 - In a browser

Open `/orders`: each row has a picture, names, a chip and the reference the notices use.
