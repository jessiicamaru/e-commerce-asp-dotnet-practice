# Quickstart: Filter the catalogue by price and by what is in stock

## Scenario 1 - Tests

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "CatalogueFilterTests|SearchIndexTests"
cd ../client && npx vitest run src/pages/catalog
```

Expected: all green. `CatalogueFilterTests` includes the two plan checks.

## Scenario 2 - Through the gateway (Bruno `catalog/`)

- `GET /api/products?maxPrice=20000000&currency=VND&pageSize=50`: every item's `price` ≤ 20,000,000.
- `GET /api/products?inStock=true&pageSize=50`: every item's `availability` is `InStock`.
- `GET /api/products?minPrice=5&maxPrice=1`: 400.

## Scenario 3 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| `maxPrice` ignored | a product over the maximum is listed |
| `minPrice` compared with `>` instead of `>=` | a product exactly at the minimum is dropped |
| The other currency uses the default price | a USD-only range lists by the VND price |
| An inactive variant's price counts | a product whose cheap variant is withdrawn is listed under it |
| `inStock` ignored | an out-of-stock product is listed |
| The per-product subquery comes back | the plan test sees a SubPlan |
| The client does not send `inStock` | catalog page test |
