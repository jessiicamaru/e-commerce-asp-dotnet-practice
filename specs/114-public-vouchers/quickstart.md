# Quickstart: Shoppers see the vouchers they could use

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "PublicVoucher"
cd ../client && npx vitest run src/components/voucher src/pages/shop-front src/pages/product src/components/checkout
```

Expected: a public shop voucher is listed for its shop and a private one is not; ended, not started, disabled, used up
and unpriced-in-the-currency ones are not; the platform's only when asked; a product-targeted one only for that product
or its variant; the order is ending-soonest; no limit or count in the answer; `isPublic` is set on create and by edit;
the quote's lines carry their seller.

## Scenario 2 - Through the gateway (Bruno)

`seller/`: the seller's voucher is created public; `GET /api/vouchers/public?sellerId=` without a token lists it; edited
to private, it is gone. `security-checks/`: no scope is 400.

## Scenario 3 - In the storefront

The shop page, the product page and checkout show the public vouchers with a copy of the code; at checkout "Use"
applies one.

## Scenario 4 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| The public filter removed | the private-voucher test |
| The date, status, used-up or currency filter removed | the not-listed test |
| The shop filter removed (all shops) | the other-shop test |
| The product filter removed | the product test |
| The order reversed | the ordering test |
| `isPublic` ignored on create or edit | the create and edit tests |
