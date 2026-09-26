# Quickstart: Validating revenue on the paid day

> Written on 2026-09-27, after the feature merged (#156), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](spec.md) | **Contract**: [http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d        # PostgreSQL for the Order tests on 5434
```

---

## Scenario 1 - The day, the fallback and the guard (US1, SC-001, SC-003)

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~RevenueDayTests"
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests        # PR: 263/263
```

**Expected**: `An_order_placed_one_day_and_paid_the_next_is_revenue_on_the_second_day` (absent from D, in D+1's totals
and days, admin and seller), `An_order_with_no_payment_time_counts_on_the_day_it_was_placed`, and
`The_payment_time_is_written_once_by_the_settlement_and_never_for_a_failure` pass.

---

## Scenario 2 - Live (SC-002)

```bash
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build order
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
docker exec -it <order-db-container> psql -U $DB_USER -d ecommerce_order_db -c \
  'SELECT "Id", "Status", "CreatedAt", "PaidAt", "PaidAt" - "CreatedAt" AS lag FROM orders ORDER BY "CreatedAt" DESC LIMIT 5;'
```

**Expected** (the PR's run): `verify-saga.sh` `approve=pass`; the orders it placed carry a `PaidAt` 0.3 to 1.3 s after
`CreatedAt`; a failed order and the order placed before the rebuild have none.

---

## Scenario 3 - The detail shows it (US2)

```bash
curl -s http://localhost:5000/api/orders/$ORDER -H "Authorization: Bearer $CUSTOMER" | jq .paidAt
```

**Expected**: the settlement time for a paid order; null otherwise. Not recorded as run by hand in the PR.
