# Quickstart: Validating the tests for untested promises

**Feature**: [spec.md](spec.md)

## Prerequisites

```bash
cd server
docker compose up -d
```

`DB_PASSWORD` (and for Catalog `SEAWEEDFS_ACCESS_KEY` / `SEAWEEDFS_SECRET_KEY`) set.

## Scenario 1 - The new tests (US1, SC-001)

```bash
cd server
dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ShopApplicationTests|FullyQualifiedName~EmailDeliveryTests"
dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~TotalsPersistenceTests"
dotnet test tests/Ecommerce.Orchestrator.Tests --filter "FullyQualifiedName~OrderStateMachineTests"
dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ReviewTests"
cd ../client
npx vitest run src/pages/admin-emails src/pages/admin-wording src/hooks/notification-wording
```

**Expected**: all green.

## Scenario 2 - The full suites (SC-003)

```bash
cd server
dotnet test
cd ../client
npx vitest run && npm run lint && npx tsc -b
```

## Scenario 3 - The staff list's paging, through the gateway (US2)

```bash
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/reviews?hidden=true&pageSize=100000" -H "Authorization: Bearer $ADMIN"   # 400
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/reviews?hidden=true&pageSize=50" -H "Authorization: Bearer $ADMIN"       # 200
```

## Scenario 4 - Mutations (SC-002)

Each applied by hand, run, and reverted.

| Row | Mutation | Expected red |
| :-- | :-- | :-- |
| 1 | `email-editor.tsx` reset back to `mutate(undefined, { onSuccess })` | admin-emails `says it was reset even when the editor is gone…` |
| 1 | `email-versions.tsx` restore back to `mutate(v.version, { onSuccess })` | admin-emails `says it was restored…` |
| 1 | `wording-editor.tsx` reset, the same | admin-wording `says it was reset…` |
| 1 | `wording-editor.tsx` restore, the same | admin-wording `says it was restored…` |
| 2 | the hook applies `data ?? {}` whenever not pending (with `isPending` in its dependencies) **and** `applyWording` stops starting from the bundle (`{ kind: overrides }`, not deep) | `keeps the bundled words when the wording cannot be fetched` (either change alone is caught by the other guard - research D3) |
| 3 | `GetMyOrderByIdQueryHandler` answers `TaxRate` from `ITaxRates` | `A_rate_changed_after_the_order_does_not_change_it` |
| 4 | the saga publishes `ProcessPaymentCommand` with `string.Empty` for the currency | `The_saga_relays_the_currency_to_Payment_and_the_variant_to_Inventory` |
| 4 | the saga relays items with `VariantId = default` | the same |
| 5 | `TryRetryAsync` without `&& e.Status == Failed` | `Simultaneous_retries_of_one_email_send_it_once` (and the sequential retry test) |
| 6 | `SellerRegisteredEvent(user.Id, "", now)` | `Approval_makes_a_seller_opens_the_shop_and_tells_Catalog_and_the_applicant_once` |
| 6 | the unique-violation `catch` never matches | `Two_simultaneous_applications_leave_one_waiting` |
| 7 | no `GetReviewsForStaffQueryValidator` (the code before) | `The_staff_list_is_paged_like_every_other` (4 cases) |
