# Quickstart: A seller's list of the returns of their parcels

## Scenario 1 - Tests

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "ReturnTests|SellerSalesTests"
cd ../client && npx vitest run src/pages/shop-returns src/pages/shop-sales
```

Expected: all green.

## Scenario 2 - Through the gateway (Bruno)

After the seller folder's return has been requested on the seller's parcel,
`GET /api/orders/sales/returns?status=Requested` as the seller contains it. As a customer the same request is 403.

## Scenario 3 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| The list ignores `SellerId` | another seller's return is listed |
| The list takes the shop's returns when the seller has none | the shop's return is listed |
| The status filter is dropped | a Received return appears under Requested |
| The badge reads any seller's return on the order | another seller's return badges this sale |
| The page opens on another tab | client test |
| A row does not link to its sale | client test |
