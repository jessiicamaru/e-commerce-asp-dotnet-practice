# Quickstart: Validating vouchers (server)

> Written on 2026-09-27, after the feature merged (#153), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature**: [spec.md](spec.md) | **Contract**: [http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build      # Order and the gateway rebuilt
login() { curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d "{\"email\":\"$1\",\"password\":\"$2\"}" | jq -r .token; }
ADMIN=$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
SELLER=$(login "$SELLER_EMAIL" "$SELLER_PASSWORD")
CUSTOMER=$(login "$CUSTOMER_EMAIL" "$CUSTOMER_PASSWORD")     # with a VND cart, an address ADDRESS and option OPTION
```

---

## Scenario 1 - Pricing rules (US1, FR-002 to FR-004)

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~VoucherPricingTests"
```

**Expected**: 21 pure tests pass - the minimum spend in words, 30% until the cap, one variant, a product target, new
customer, free delivery up to its cap, shop vouchers on their own lines first, stacking limits, no amount row refused
even for a percentage, fixed never more than its lines, proportional spread with the remainder to the largest, cents
half away from zero, unknown and disabled read the same, dates and limits in words, minimum quantity and first order in
the shop, case and more than five codes, no codes, tax on the discounted price with the parts summing to the total, a
discount larger than its line thrown as a programming error.

---

## Scenario 2 - Checkout, races and release against PostgreSQL (US1, US3, SC-001 to SC-003)

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~VoucherCheckoutTests|FullyQualifiedName~VoucherManagementTests"
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests          # PR: 260/260
```

**Expected**: 14 checkout tests (quote and order agree and the order freezes the discounts; free delivery and its tax;
a shop voucher out of the seller's terms and a platform voucher not; a refused voucher places nothing; many checkouts
for the last use place one; one customer racing themselves gets one order; the claim refuses past the customer's limit
and past the total whatever pricing said; releasing twice gives one use back; a failed and a cancelled order give it
back; new customer refused after a sold order; a returned parcel refunds after the discount; a seller's revenue less
their own voucher) and 5 management tests pass. The fixture retries as production does (research D7); without the
execution-strategy fix 12 voucher tests fail.

---

## Scenario 3 - Through the gateway (US1, US2)

```bash
CODE="QS-$(date +%s)"
curl -s -X POST http://localhost:5000/api/vouchers -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d "{\"code\":\"$CODE\",\"name\":\"Ten percent\",\"benefit\":\"Percent\",\"percent\":10,\"amounts\":[{\"currency\":\"VND\",\"maxDiscount\":50000}]}" \
  | jq '.isPlatform, .status'                                                   # true, "Active"
curl -s "http://localhost:5000/api/orders/quote?addressId=$ADDRESS&shippingOption=$OPTION&voucherCodes=$CODE" \
  -H "Authorization: Bearer $CUSTOMER" | jq '.discountTotal, .vouchers, .totalAmount'
curl -s -o /dev/null -w '%{http_code}\n' \
  "http://localhost:5000/api/orders/quote?addressId=$ADDRESS&shippingOption=$OPTION&voucherCodes=NO-SUCH-CODE" \
  -H "Authorization: Bearer $CUSTOMER"                                          # 409
curl -s -X POST http://localhost:5000/api/orders -H "Authorization: Bearer $CUSTOMER" -H 'Content-Type: application/json' \
  -d "{\"addressId\":\"$ADDRESS\",\"shippingOption\":\"$OPTION\",\"voucherCodes\":[\"$CODE\"]}" | jq '.discountTotal, .totalAmount'
```

**Expected**: the order's total equals the quote's, with the voucher's amount as its discount.

Seller side:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/vouchers -H "Authorization: Bearer $SELLER" \
  -H 'Content-Type: application/json' \
  -d '{"code":"FREE-SHIP-X","name":"x","benefit":"FreeShipping","amounts":[{"currency":"VND"}]}'   # 400
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/vouchers/mine -H "Authorization: Bearer $CUSTOMER"   # 403
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/vouchers/mine                                          # 401
```

Bruno runs all of this as `order/` (create, quote, unknown code 409, checkout claims it) and `seller/` (create, list, no
free delivery, disable, disable again 409, customer 403, no token 401):

```bash
cd bruno && npx @usebruno/cli run --env local --env-var "baseUrl=http://localhost:8088" \
  --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
# PR: 229/229 requests, 376/376 tests
```

---

## Scenario 4 - Across databases

```bash
docker exec -it <order-db-container> psql -U $DB_USER -d ecommerce_order_db -c \
  'SELECT o."Status", o."Subtotal", o."ShippingPrice", o."TaxTotal", o."DiscountTotal", o."TotalAmount", r."Code", r."Amount", v."UsedCount"
     FROM orders o JOIN voucher_redemptions r ON r."OrderId" = o."Id" JOIN vouchers v ON v."Id" = r."VoucherId"
    ORDER BY o."CreatedAt" DESC LIMIT 1;'
docker exec -it <payment-db-container> psql -U $DB_USER -d ecommerce_payment_db -c \
  'SELECT "Amount", "Currency", "Status" FROM payments ORDER BY "ProcessedAt" DESC LIMIT 1;'
```

**Expected** (the PR's run): `Shipped | 2998000 + 60000 + 300800 - 50000 = 3308800 | BRUNO-… 50000 | UsedCount 1` and
Payment `3308800.00 | VND | Approved`. The tax, 300,800, is 10% of the goods after discount plus delivery.
