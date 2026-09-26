# Quickstart: Validating admin revenue less returns

> Written on 2026-09-27, after the feature merged (#176), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d    # PostgreSQL for the tests; the full stack for scenario 3
# for scenario 3, with the stack up:
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

---

## Scenario 1 - The test that was red first (SC-001 to SC-003)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests \
  --filter "FullyQualifiedName~The_admin_overview_leaves_a_received_return_out_the_way_the_sellers_page_does"
```

**Expected**: green. It places one order with two sellers' parcels (20,000 and 5,000) and another with an open
return, receives a return of the first seller's parcel with a refund of 22,000 (her goods and their tax), and
asserts: the Overview's revenue is both totals less 22,000 with 2 orders; her product is gone from top products and
his is there; the buyer spent the order's total less 22,000; and her revenue on her own page is 300,000 (only the
open return's order), his 5,000. The pull request records it red first, by exactly the 22,000.

## Scenario 2 - The mutations (SC-004)

Remove, one at a time, in `OrderInsights.cs`: the subtraction in `RevenueByDayAsync`, the `Where` that drops a
returned parcel's lines in `ProductSalesAsync`, the subtraction in `BuyersAsync`. Each makes scenario 1 red
(recorded in the pull request). Restore each before the next.

## Scenario 3 - Against the running stack

With a parcel whose return has been received (Bruno's `admin-audit/staff receive it and the units go back on the shelf`
leaves one), compare:

```sql
-- ecommerce_order_db on localhost:5434
SELECT o."Id", o."TotalAmount", o."Currency", r."RefundAmount",
       (COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS shop_day
FROM parcel_returns r JOIN orders o ON o."Id" = r."OrderId"
WHERE r."Status" = 'Received' ORDER BY o."CreatedAt" DESC LIMIT 5;
```

```bash
curl -fsS "http://localhost:5000/api/orders/insights/revenue?from=<shop_day>&to=<shop_day>" \
  -H "Authorization: Bearer $ADMIN" | jq '.days'
```

**Expected**: that day's revenue in that currency equals the sum of `TotalAmount` of that day's sold orders less the
sum of `RefundAmount` above. Whether this was run by hand is not recorded; Bruno against the rebuilt Order container
passed **267/267 requests, 437/437 tests**.
