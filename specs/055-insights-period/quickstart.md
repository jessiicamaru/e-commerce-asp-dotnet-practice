# Quickstart: Validating one insights period

> Written on 2026-09-27, after the feature merged (#138), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d        # Order's database on 5434, Catalog's on 5433
```

For scenario 4, the stack running and `ADMIN` holding an administrator's token.

## Scenario 1 - Order's three queries (US1 scenario 2, US2, SC-002, SC-003)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~InsightsTests"
```

**Expected**: green, including `A_period_is_whole_days_from_the_day_from_falls_on_to_the_day_to_falls_on`,
`A_single_day_is_a_period` and `Every_insight_refuses_more_than_366_days_and_a_period_that_ends_before_it_starts`.
All three failed before the fix. The whole project: 183 tests at the merge.

## Scenario 2 - Catalog's top viewed (US2, SC-002, SC-003)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ProductViewTests"
```

**Expected**: green, including `Top_viewed_counts_whole_days_whatever_time_the_ends_name` and
`Top_viewed_refuses_more_than_366_days_like_every_insight`, which failed before the fix. The whole project: 155
tests at the merge.

## Scenario 3 - The Overview asks for the days it draws (US1 scenario 1, SC-001)

```bash
cd client
npx vitest run src/pages/admin-overview
```

**Expected**: `asks for exactly the days the chart draws` passes. Before the fix it failed with
`expected 8 to be 7`.

## Scenario 4 - Through the gateway

```bash
curl -s "http://localhost:5000/api/orders/insights/top-products?from=2024-01-01T00:00:00Z&to=2025-12-31T00:00:00Z" \
  -H "Authorization: Bearer $ADMIN" | jq .status,.errors
curl -s "http://localhost:5000/api/products/insights/top-viewed?from=2026-09-10T00:00:00Z&to=2026-09-01T00:00:00Z" \
  -H "Authorization: Bearer $ADMIN" | jq .status,.errors
curl -s "http://localhost:5000/api/orders/insights/revenue?from=2026-09-18T15:00:00Z&to=2026-09-24T03:00:00Z" \
  -H "Authorization: Bearer $ADMIN" | jq .from,.to
```

**Expected**: `400` with "at most 366 days"; `400` with "must not start after it ends"; and
`"2026-09-18T00:00:00Z"`, `"2026-09-25T00:00:00Z"`. Bruno's `admin-insights/a period longer than 366 days is
refused` makes the first check in the collection run. The curl calls were not recorded as run for this feature.

## Scenario 5 - Mutation checks

The pull request's, each restored:

| Mutation | Expected |
| :-- | :-- |
| `End` is the last day, not the day after | 2 red |
| Top products without the period rule | 1 red |
| No day limit | 1 red |
