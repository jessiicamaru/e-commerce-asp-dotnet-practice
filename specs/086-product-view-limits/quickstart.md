# Quickstart: Validating honest view counts

> Written on 2026-09-27, after the feature merged (#178), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
P=<a listed product id>
```

---

## Scenario 1 - One visitor, one view (SC-001)

```bash
V=$(uuidgen)
for i in 1 2 3; do
  curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/products/$P/view \
    -H 'Content-Type: application/json' -d '{"viewer":"'$V'"}'       # 204 each time
done
```

```sql
-- ecommerce_catalog_db on localhost:5433
SELECT "Day", "Views" FROM product_views WHERE "ProductId" = '<P>' ORDER BY "Day" DESC LIMIT 1;   -- rose by 1
SELECT "Day", length("Viewer") FROM product_viewers WHERE "ProductId" = '<P>';                       -- today, 64
```

A call with no body (`curl -X POST .../view`) still counts each time, as before.

## Scenario 2 - The gateway limit (SC-003)

```bash
for i in $(seq 1 32); do
  curl -s -o /dev/null -w '%{http_code} ' -X POST http://localhost:5000/api/products/$P/view
done; echo
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/products/$P                     # 200
```

**Expected**: 204 until the 30th call of the minute, then 429 with a `Retry-After` header; the product read is 200.
This assumes nothing else from the same address called the view endpoint in that minute. Whether it was run by hand
is not recorded; the gateway test below is the recorded evidence.

## Scenario 3 - The tests (SC-001 to SC-004, SC-006)

```bash
cd server
DB_PASSWORD=<your password> SEAWEEDFS_ACCESS_KEY=... SEAWEEDFS_SECRET_KEY=... \
  dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~ProductViewTests"
dotnet test tests/Ecommerce.ApiGateway.Tests \
  --filter "FullyQualifiedName~Counting_product_views_too_fast_is_429_and_nothing_else_about_products_is_limited"
cd ../client && npm test -- src/services/product
```

**Expected** at the merge: Catalog 210/210 (among them `One_visitor_opening_the_page_twenty_times_at_once_counts_once`,
`A_signed_in_shopper_counts_once_whatever_visitor_id_they_send`, `A_visitor_naming_nobody_counts_every_time`,
`A_products_viewers_from_earlier_days_go_with_its_first_view_today`), ApiGateway 13/13, client 460/460. The pull
request records five mutations, each red: no viewer at all; the body winning over the token; no pruning; no gateway
policy on the route; the stored id never reused.

## Scenario 4 - Bruno (SC-005)

Run the collection (see `CLAUDE.md`). `admin-insights/a shopper opens the product page` opens this run's product
twice as the same visitor, and `admin-insights/the most viewed products` asserts exactly one view. Recorded at the
merge: **268/268 requests, 439/439 tests**.
