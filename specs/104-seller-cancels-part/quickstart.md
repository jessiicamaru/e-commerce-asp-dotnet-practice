# Quickstart: Validating part cancellation

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

⚠️ Deploy Inventory and Payment before Order (research D8).

## Scenario 1: The tests (SC-001 to SC-004)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~PartCancellationTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter "FullyQualifiedName~RestockCancelledPartTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Payment.Tests --filter "FullyQualifiedName~PartRefundTests"
cd ../client
npx vitest run src/pages/shop-sale src/pages/order
```

**Expected**: everything passes.

## Scenario 2: Through the gateway (SC-005)

Run the Bruno `seller/` requests for the part cancel, and `security-checks/` for 401. By hand, as the seller of a paid
order:

```bash
curl -fsS -X POST http://localhost:5000/api/orders/sales/$ORDER/cancel -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' -d '{"reason":"Out of stock"}' | jq '.parts'
```

**Expected**: the part shows `cancelledBy: "Seller"`. Within seconds Payment has one refund with that `PartId`, and the
variant's stock is back.

## Scenario 3: The saga still runs

```bash
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

## Scenario 4: Mutations (SC-006)

Apply each change below on its own, run the suite named beside it, and expect it to go red. Then restore the file and
`touch` it.

| Mutation | Suite |
| :-- | :-- |
| The cancel's `FOR UPDATE` removed | `PartCancellationTests` (concurrent last parts) |
| The summary counts cancelled parts | `PartCancellationTests` |
| `Earning` counts cancelled parts | `PartCancellationTests` |
| Whole-order refund ignores part refunds | `PartRefundTests` |
| Restock ignores the variant filter | `RestockCancelledPartTests` |
