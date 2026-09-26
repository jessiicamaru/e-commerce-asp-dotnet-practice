# Quickstart: Validating insights in the shop's days

> Written on 2026-09-27, after the feature merged (#170), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

---

## Scenario 1 - The period starts at Hanoi midnight (SC-002)

```bash
curl -fsS "http://localhost:5000/api/orders/insights/revenue" -H "Authorization: Bearer $ADMIN" \
  | jq '{from, to, firstDay, lastDay}'
```

**Expected**: `from` is `<firstDay minus one day>T17:00:00Z`, `to` is `<lastDay>T17:00:00Z`, and `lastDay` is
today's date in Hanoi. Every `days[].day` lies between `firstDay` and `lastDay`.

## Scenario 2 - A bare date is the shop's day (FR-002)

```bash
curl -fsS "http://localhost:5000/api/orders/insights/revenue?from=2026-09-26&to=2026-09-26" \
  -H "Authorization: Bearer $ADMIN" | jq '{from, firstDay, lastDay}'
```

**Expected**: `firstDay` and `lastDay` are both `2026-09-26`, and `from` is `2026-09-25T17:00:00Z`.

## Scenario 3 - A late-night UTC order counts on the next morning (SC-001)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests \
  --filter "FullyQualifiedName~An_order_paid_late_at_night_UTC_counts_on_the_next_morning_in_Hanoi"
```

**Expected**: green. The pull request records the whole project at 264/264, and that grouping by the UTC date in
`OrderInsights` turned this test and one existing insight test red.

To see the same thing in the database, for an order paid in the evening UTC (the `::time` cast reads the session's
time zone, UTC in the compose containers unless changed):

```sql
-- ecommerce_order_db on localhost:5434
SELECT "Id", "PaidAt", ("PaidAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS shop_day
FROM orders WHERE "PaidAt"::time >= '17:00' ORDER BY "PaidAt" DESC LIMIT 5;
```

`shop_day` is the day after the UTC date of `PaidAt` - the day the Overview files it under.

## Scenario 4 - The chart draws the server's days (SC-004)

```bash
cd client
npm test -- src/utils/insights src/pages/admin-overview
```

**Expected**: green. The pull request records 456/456 client tests, and that the Overview ignoring
`firstDay`/`lastDay` fails the page test - after its mocked days were moved far from today, because the first
version of the test survived the mutation.

## Scenario 5 - Bruno (SC-002, SC-003, SC-005)

Run the collection (see `CLAUDE.md`, Bruno collection). `admin-insights/revenue per currency` checks every day lies
between `firstDay` and `lastDay` and that `from + 7 h = firstDay`; `seller/a seller sees their own revenue` checks
the six keys, 30 days, and the same midnight. Recorded at the merge: **267/267 requests, 435/435 tests**.

## Scenario 6 - An unknown zone stops the service (FR-001)

Start Order with `Insights__TimeZone=Mars/Olympus` (for example
`Insights__TimeZone=Mars/Olympus dotnet run --project src/Services/Order/Ecommerce.Order.WebApi/`).

**Expected**: the process exits at startup with `Insights:TimeZone 'Mars/Olympus' is not a time zone this machine
knows. Use an IANA id such as 'Asia/Ho_Chi_Minh'.` Whether this was run by hand is not recorded.
