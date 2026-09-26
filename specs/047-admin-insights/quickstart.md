# Quickstart: Validating admin insights

> Written on 2026-09-27, after the feature merged (#99, #101), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](spec.md) | **Contract**: [http-api.md](contracts/http-api.md)

How to show the feature works. Each scenario names what it proves. The results quoted are the ones recorded in
#99 and #101; nothing here was re-run for this record.

---

## Prerequisites

```bash
cd server
docker compose up -d                  # the Postgres containers, RabbitMQ, Mailpit ...
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ \
                          --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/   # AddProductViews
./start-dev.sh                        # every service, the gateway on 5000
```

Order and Identity need no migration for this feature. Tokens, through the gateway:

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
CUSTOMER=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"<a customer>","password":"<their password>"}' | jq -r .token)
```

A moderator's token is obtained the same way for an account an administrator has granted `Moderator`
(`PUT /api/users/{id}/roles/Moderator`, specs/043).

---

## Scenario 1 - Revenue per currency, sales only (US1, FR-001, FR-004, FR-005, SC-003)

```bash
cd server
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests --filter FullyQualifiedName~InsightsTests
```

**Expected**: 3 pass (#99): `Revenue_counts_paid_orders_per_currency_and_nothing_else`,
`Top_products_count_units_and_keep_revenue_per_currency`, `Top_buyers_rank_by_spend_in_the_asked_currency`.
Paid, Shipped and Preparing orders count; Cancelled, Failed and Submitted do not; VND and USD are separate
totals equal to the stored `TotalAmount` of the counted orders; one day row per currency per day; the cancelled
order's 50 units are not in top products. The suite was 179/179 at the merge.

Through the gateway:

```bash
curl -fsS "http://localhost:5000/api/orders/insights/revenue" -H "Authorization: Bearer $ADMIN" | jq
curl -fsS "http://localhost:5000/api/orders/insights/revenue?from=2026-09-01T00:00:00Z&to=2026-09-24T00:00:00Z" \
  -H "Authorization: Bearer $ADMIN" | jq '.totals'
```

**Expected**: `200`; one entry in `totals` per currency, never one combined figure; `days` holds only days with
sales.

Cross-check against the database (`ecommerce_order_db`, host port 5434):

```bash
docker exec -it ecommerce-order-db psql -U "$DB_USER" -d ecommerce_order_db -c "
SELECT COALESCE(\"Currency\", '(default)') AS currency, count(*) AS orders, sum(\"TotalAmount\") AS revenue
FROM orders
WHERE \"Status\" IN ('Paid','Completed','Preparing','Shipped')
  AND \"CreatedAt\" >= now() - interval '30 days' AND \"CreatedAt\" < now()
GROUP BY 1;"
```

**Expected**: the same figures as the default `revenue` call made at the same moment (null currency is reported
under the default currency by the API).

## Scenario 2 - The period rules (FR-011, research D12)

```bash
curl -s -o /dev/null -w '%{http_code}\n' \
  "http://localhost:5000/api/orders/insights/revenue?from=2024-01-01T00:00:00Z&to=2026-01-01T00:00:00Z" \
  -H "Authorization: Bearer $ADMIN"
curl -s -o /dev/null -w '%{http_code}\n' \
  "http://localhost:5000/api/orders/insights/top-buyers?from=2026-09-10T00:00:00Z&to=2026-09-01T00:00:00Z" \
  -H "Authorization: Bearer $ADMIN"
curl -s -o /dev/null -w '%{http_code}\n' \
  "http://localhost:5000/api/orders/insights/top-products?by=price" -H "Authorization: Bearer $ADMIN"
```

**Expected**: `400` each - more than 366 days, a period ending before it starts, an unknown `by`. At the merge a
two-year period on `top-products`, `top-buyers` or `top-viewed` is **accepted** (only revenue is capped); that is
the inconsistency specs/055 removed. No unit test covers these refusals (not recorded as tested).

## Scenario 3 - What sells and who buys (US2, FR-006, FR-007, FR-003)

```bash
curl -fsS "http://localhost:5000/api/orders/insights/top-products?by=units&limit=5" -H "Authorization: Bearer $ADMIN" | jq
curl -fsS "http://localhost:5000/api/orders/insights/top-products?by=revenue&currency=VND&limit=5" -H "Authorization: Bearer $ADMIN" | jq
IDS=$(curl -fsS "http://localhost:5000/api/orders/insights/top-buyers?currency=VND&limit=5" \
  -H "Authorization: Bearer $ADMIN" | jq -r '[.[].customerId | "ids=" + .] | join("&")')
curl -fsS "http://localhost:5000/api/users/lookup?$IDS" -H "Authorization: Bearer $ADMIN" | jq
```

**Expected**: the first list in descending units, the second in descending VND revenue; each product's `revenue`
has at most one entry per currency; buyers are ids, and the lookup returns their emails, leaving out any id nobody holds.

## Scenario 4 - Views: counted once each, and only for shoppers (US2, FR-002, FR-008, SC-004)

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Catalog.Tests --filter FullyQualifiedName~ProductViewTests
```

**Expected**: 3 pass (#99): twenty simultaneous views count twenty; the seller, a moderator, a pending product and
an unknown id count nothing; the most viewed come first. The suite was 152/152 at the merge.

Through the gateway, with a listed product id `$P`:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/products/$P/view"                       # anonymous
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/products/$P/view" -H "Authorization: Bearer $ADMIN"
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/products/$(uuidgen)/view"
curl -fsS "http://localhost:5000/api/products/insights/top-viewed?limit=50" -H "Authorization: Bearer $ADMIN" | jq
```

**Expected**: `204` three times; the product's count for today rose by exactly one (the anonymous view), not by
the administrator's and not for the made-up id:

```bash
docker exec -it ecommerce-catalog-db psql -U "$DB_USER" -d ecommerce_catalog_db -c \
  "SELECT \"ProductId\", \"Day\", \"Views\" FROM product_views WHERE \"ProductId\" = '$P' ORDER BY \"Day\";"
```

## Scenario 5 - People in numbers (US3, FR-012)

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Identity.Tests --filter FullyQualifiedName~UserReportTests
curl -fsS "http://localhost:5000/api/users/stats" -H "Authorization: Bearer $ADMIN" | jq
```

**Expected**: 2 pass (#99): ids become emails and unknown ones are left out; registering a customer and
approving a seller raise `total` and `customers` by 2 and `sellers` by 1. The suite was 73/73 at the merge. The
`curl` returns all seven counts.

## Scenario 6 - Administrators only (FR-010, SC-002)

```bash
for url in orders/insights/revenue orders/insights/top-products orders/insights/top-buyers \
           products/insights/top-viewed users/stats "users/lookup?ids=$(uuidgen)"; do
  printf '%-40s anon=%s customer=%s admin=%s\n' "$url" \
    "$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:5000/api/$url")" \
    "$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:5000/api/$url" -H "Authorization: Bearer $CUSTOMER")" \
    "$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:5000/api/$url" -H "Authorization: Bearer $ADMIN")"
done
```

**Expected**: `401`, `403`, `200` on every line; the same `403` with a moderator's token. This loop is wider
than what the Bruno collection asserts (see Scenario 7); running it was not recorded.

## Scenario 7 - The Bruno collection (SC-002, SC-005)

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected** (#99): 190/190 requests, 307 tests. `admin-insights/` (folder `seq: 15`): revenue per currency,
top selling products, top buyers, who the buyers are (the email equals the collection's `customerEmail`), a
shopper opens the product page (`204`), the most viewed products, people in numbers - each `200` to an
administrator; a customer `403` on `revenue`, a moderator `403` on `top-viewed`.
`security-checks/insights without a token is 401.yml`: `401` on `revenue`. The "top" requests assert order and
shape, not that this run's product is listed.

## Scenario 8 - The Overview in the storefront (US1-US3, FR-009, FR-013)

```bash
cd client
npm test -- src/pages/admin-overview src/utils/insights src/pages/product src/layouts/admin-layout
```

**Expected**: pass - revenue shown per currency and never summed; top buyers named by email; the waiting count
links to its queue; choosing another period asks again; the product page reports a view exactly once however
often it renders; the Overview link is first for an administrator and absent for a moderator; the chart draws one
column per day with empty days included, scales bars to the highest day and names it, shows one currency at a
time, and says the day, amount and orders on hover; `periodDays` crosses a month end. Whole client suite: 225/225
at #99, 232/232 at #101 (three full runs in a row); lint, type-check and build clean.

By hand: sign in as an administrator, open `http://localhost:5173/admin/overview`, choose 7, 30 and 90 days, click
a currency card, hover a column. Recorded in the PRs as screenshots at 1360px and 390px (#99) and at 1500px,
hovering the last day, and 390px (#101), with no overflow at 390px.

## Scenario 9 - Mutation checks (SC-001)

Recorded in #99, each red and then restored (with `touch`, so MSBuild rebuilt the restored file):

1. Add `OrderStatus.Cancelled` to `OrderInsights.Sold` - `Revenue_counts_paid_orders_per_currency_and_nothing_else`
   and `Top_products_count_units_and_keep_revenue_per_currency` fail.
2. Remove the staff check from `ProductViewHandlers.Handle(RecordProductViewCommand)` -
   `Staff_and_the_seller_do_not_count_and_neither_does_what_is_not_on_the_shelf` fails.

The exact edits used are not recorded; the two above are the smallest that match the PR's descriptions.

## Scenario 10 - Nothing else broke

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: passes (#99). The feature adds no message and changes no saga step; this confirms it.
