# Quickstart: Checkout says how payment works and how long delivery takes

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "DeliveryEstimate|DeliverySettings"
cd ../client && npx vitest run src/pages/checkout src/pages/admin-delivery src/components/checkout
```

Expected: all green.

## Scenario 2 - In a browser

1. `/admin/delivery`: give Express 1-2 days and save. Expected: "Saved"; the audit log shows `DeliveryOptionSaved`.
2. `/checkout`: Express reads "1-2 business days"; the Payment card says the order is charged when placed and - with
   the stub running - that no money is moved.
3. Set a minimum above the maximum. Expected: refused, naming the field.
