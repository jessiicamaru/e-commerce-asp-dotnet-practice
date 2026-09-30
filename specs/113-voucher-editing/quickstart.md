# Quickstart: A voucher's terms can be corrected

## Scenario 1 - Tests

```bash
cd server && DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "VoucherEditing"
cd ../client && npx vitest run src/components/voucher src/pages/shop-vouchers
```

Expected: an extended end date applies and an earlier order's redemption is unchanged; a total under the uses is 409
and changes nothing; lowering a minimum subtotal lets a checkout qualify (through `CheckoutPricing`); a disabled voucher
is 409; another seller's voucher is 404, a seller's voucher to an administrator is 404; the audit entry carries before
and after; an edit and many claims at once never exceed the limit.

## Scenario 2 - Through the gateway (Bruno `seller/`)

The seller's voucher from specs/070's requests: edit its end date and total limit - 200 with the new terms; a limit
below its uses - 409; a customer - 403 (`security-checks`).

## Scenario 3 - In the storefront

`/shop/vouchers`: "Edit" on an active voucher opens its terms prefilled; save; the list shows the new end date.

## Scenario 4 - Mutations (each must turn a test red)

| Mutation | Caught by |
| :-- | :-- |
| The `UsedCount <= total` guard removed | the below-the-uses test and the race test |
| The owner condition removed | the other-seller test |
| An administrator allowed on a seller's voucher | the administrator test |
| The status guard removed | the disabled test |
| The audit entry not staged | the audit test |
| The minimum subtotal not written | the checkout-qualifies test |
