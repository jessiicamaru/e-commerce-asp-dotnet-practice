# Quickstart: Validating seller insights

> Written on 2026-09-27, after the feature merged (#152), from the code at that merge, the pull request and
> docs/features/seller-insights.md.

**Feature**: [spec.md](spec.md) | **Contract**: [http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
login() { curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d "{\"email\":\"$1\",\"password\":\"$2\"}" | jq -r .token; }
SELLER=$(login "$SELLER_EMAIL" "$SELLER_PASSWORD")        # an approved seller (specs/044)
CUSTOMER=$(login "$CUSTOMER_EMAIL" "$CUSTOMER_PASSWORD")
```

---

## Scenario 1 - The figures are right (US1, US2, SC-001, SC-002)

The revenue rules need two sellers, shared orders, returns and currencies - built in the tests, not by hand:

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests   --filter "FullyQualifiedName~SellerInsightsTests|FullyQualifiedName~InsightsTests"
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Catalog.Tests --filter "FullyQualifiedName~SellerProductInsightsTests"
```

**Expected**: 11 `SellerInsightsTests` (own lines before tax, another seller never, sold only, a received return out
and an open one in, a return is one seller's part, an order counted once, per currency, whole days, the period rule,
top products, a caller without an id refused) and 6 `SellerProductInsightsTests` (own views in the period most viewed
first, the weighted rating, null with nothing, the list limited but not the totals, the period rule, no id refused).
The admin's `InsightsTests` still pass on the shared grouping. PR: Order 220/220, Catalog 167/167.

---

## Scenario 2 - The endpoints through the gateway (FR-001, FR-002, SC-003)

```bash
curl -s http://localhost:5000/api/orders/sales/insights/revenue -H "Authorization: Bearer $SELLER" | jq '.totals, (.days|length)'
curl -s "http://localhost:5000/api/orders/sales/insights/top-products?limit=5" -H "Authorization: Bearer $SELLER" | jq length
curl -s "http://localhost:5000/api/products/insights/mine?limit=5" -H "Authorization: Bearer $SELLER" | jq '.views, .ratingAverage'
for u in orders/sales/insights/revenue products/insights/mine; do
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/api/$u -H "Authorization: Bearer $CUSTOMER"
done                                                                                   # 403, 403
curl -s -o /dev/null -w "%{http_code}\n" \
  "http://localhost:5000/api/orders/sales/insights/revenue?from=2024-01-01&to=2026-01-01" -H "Authorization: Bearer $SELLER"
# 400 - longer than 366 days
```

**Expected**: 200 with the shapes in the contract; a seller with no sales gets empty `totals` and `days`; 403 for the
customer; 400 for the long period. Bruno runs the same as `seller/` 56-59:

```bash
cd bruno && npx @usebruno/cli run --env local --env-var "baseUrl=http://localhost:8088" \
  --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
# PR: 219/219 requests, 359/359 tests, through the storefront's nginx
```

---

## Scenario 3 - The page (FR-003, FR-005)

```bash
cd client
npm test -- src/pages/shop-insights src/services/insights src/utils/insights src/components/insights
npm test              # PR: 378/378 in 63 files
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8088/shop/insights       # 200, deep link
```

**Expected**: the page asks both services for the seller's own figures (no seller id in the URLs), shows revenue per
currency never added together, shows the weighted rating and each product's views and rating, says "Nothing yet."
rather than drawing zeros, and asks again when another period is chosen. Clicking through in a browser was not done at
merge.
