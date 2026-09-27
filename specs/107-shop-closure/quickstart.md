# Quickstart: A seller pauses their shop, and staff close one

## Scenario 1 - Tests

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "ShopClosureTests|SellerSuspensionTests|ShopPageTests"
cd ../client && npm test -- shop admin-shops shop-front
```

Expected: all green.

## Scenario 2 - Through the gateway (Bruno `seller/`)

1. The seller pauses: 200, `state: Paused`. The product is gone from `GET /api/products?sellerId=`. The shop page says
   `paused: true`. Pausing again is 409.
2. The seller reopens: 200, `state: Open`, and the product is listed again.
3. A customer closing the shop gets 403. The administrator closes it with a reason: 200, `state: Closed`, and the shop
   page is a 404.
4. The seller tries to reopen: 409 naming the closure. `GET /api/shops/mine` shows the reason.
5. `GET /api/shops/closed` contains the shop. Staff reopen it: 200, `state: Open`, and the product is listed again.

## Scenario 3 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| `ApplyShopStateAsync` ignores `PausedAt` | pausing leaves products on the shelf |
| The ban path writes its own bool again | a ban lifted while paused reopens the shop |
| Approval does not take the shop's state | a product approved while paused goes on sale |
| The seller's reopen drops `"ClosedAt" IS NULL` | the seller reopens a closed shop |
| Staff reopen clears `PausedAt` | a paused-and-closed shop reopens fully |
| The public shop page returns a closed shop | a closed shop's page answers |
| No saver notice on reopen | savers are not told |
| The client's Reopen shown for a closed shop | shop card test |
