# Quickstart: Long staff lists can be searched and filtered

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter VoucherFilter && dotnet test tests/Ecommerce.Identity.Tests --filter UserFilter
cd ../client && npx vitest run src/components/voucher src/pages/admin-users src/pages/admin-categories src/pages/admin-order-search
```

## Scenario 2 - In a browser

`/admin/vouchers`: type a code, switch to Ended; `/admin/users`: Moderators only; `/admin/categories`: search; `/admin/orders/find`: Failed tab counts and reasons in words.
