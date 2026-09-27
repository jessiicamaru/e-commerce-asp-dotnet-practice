# Quickstart: Validating payout accounts

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

## Scenario 1: The tests (SC-001 to SC-003)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~PayoutAccountTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~PayoutTests"
cd ../client
npx vitest run src/pages/shop-payouts src/pages/admin-payouts
```

## Scenario 2: Through the gateway (SC-004)

Run Bruno's `seller/` requests (set and read the account) and `security-checks/`. By hand, as the seller:

```bash
curl -fsS -X PUT http://localhost:5000/api/sellers/me/payout-account -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' -d '{"bankName":"Vietcombank","accountHolder":"NGUYEN VAN A","accountNumber":"0071 0012 34321"}'
```

**Expected**: the response masks the number (`•••• 4321`), Mailpit holds a "PayoutAccountChanged" email, and the audit
entry has no full number.

## Scenario 3: Mutations (SC-005)

Apply each change below on its own, run the suite named beside it, and expect it to go red. Then restore the file and
`touch` it.

| Mutation | Suite |
| :-- | :-- |
| The seller read returns the full number | `PayoutAccountTests` |
| The audit records the full number | `PayoutAccountTests` |
| `RecordPayout` records without an account | Order `PayoutTests` |
| The gRPC service's `Authorize` removed | `PayoutAccountTests` |
