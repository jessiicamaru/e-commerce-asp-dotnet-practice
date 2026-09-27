# Quickstart: Validating the low-stock notice

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

## Scenario 1: The tests (SC-001 to SC-003)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter "FullyQualifiedName~LowStockTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~LowStockNoticeTests"
cd ../client
npx vitest run src/components/seller/variant-editor
```

**Expected**: everything passes.

## Scenario 2: Through the gateway (SC-004)

Bruno's `seller/` and `security-checks/` threshold requests pass. To check by hand:

```bash
curl -fsS -X PUT "http://localhost:5000/api/stock/$VARIANT/low-stock-threshold" -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' -d '{"threshold":10}' | jq '{lowStockThreshold, lowStockThresholdIsDefault}'
```

**Expected**: `{ "lowStockThreshold": 10, "lowStockThresholdIsDefault": false }`.

## Scenario 3: End to end (US1)

1. As a seller, set a variant's stock to 6. Its threshold is the default, 5.
2. As a customer, check out 2 of it.
3. As the seller, open the bell. It shows "“…” is running low: 4 left." Check out 1 more; no second notice arrives.

## Scenario 4: Mutations (SC-005)

Apply each change below on its own, run the suite named beside it, and expect it to go red. Then restore the file.

| Mutation | Suite |
| :-- | :-- |
| `after < threshold` becomes `after <= threshold` | `LowStockTests` |
| `before >= threshold` dropped | `LowStockTests` |
| `threshold > 0` dropped | none - an **equivalent mutant**: with a line of 0, `after < 0` can never hold, because the CHECK keeps available stock at 0 or more. The guard stays as a statement of intent |
| 0 read as "no choice" (`LowStockThreshold is > 0 ? … : DefaultThreshold`) | `LowStockTests` - the real way 0 could break |
| `LowStockThreshold ??` ignored (always the default) | `LowStockTests` |
| The consumer notifies when `SellerId` is null | `LowStockNoticeTests` |
| `pendingChanges` never clears a line from an emptied box | client `utils/logic.test.ts` |
| The variant editor never sends the line | client `pages/shop-product` |
| `describeNotification` does not fill `left` | client `utils/notifications` |
