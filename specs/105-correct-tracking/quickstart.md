# Quickstart: Validating a tracking correction

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Scenario 1: The tests (SC-001, SC-002)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~TrackingCorrectionTests"
cd ../client
npx vitest run src/pages/shop-sale src/pages/admin-order
```

## Scenario 2: Through the gateway (SC-003)

Bruno's `admin-audit/` (on the delivered order: 409), `seller/` (another's part: 404) and `security-checks/` (401).

## Scenario 3: Mutations (SC-004)

Apply each change below on its own, run `TrackingCorrectionTests`, and expect it to go red. Then restore the file and
`touch` it.

| Mutation | Expected |
| :-- | :-- |
| `"DeliveredAt" IS NULL` removed from the guard | the delivered case fails |
| `ShippedAt` set to now in the correction | the delivery-clock case fails |
