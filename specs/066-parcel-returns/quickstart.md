# Quickstart: Validating parcel returns (server)

> Written on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature**: [spec.md](spec.md) | **Contracts**: [http-api.md](contracts/http-api.md), [messages.md](contracts/messages.md)

Each scenario maps to a story or success criterion. Scenarios 1-4 are what Bruno's `admin-audit/` folder runs on the
shop's own parcel; the PR records Bruno at 215/215 requests and 352/352 tests. Whether each curl below was run by hand
as written is not recorded.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
```

Order, Payment, Inventory, Catalog, Cart, Identity, the Orchestrator and the gateway must all be up - a return starts
from a paid, delivered order. Tokens through the gateway:

```bash
login() { curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d "{\"email\":\"$1\",\"password\":\"$2\"}" | jq -r .token; }
ADMIN=$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
CUSTOMER=$(login "$CUSTOMER_EMAIL" "$CUSTOMER_PASSWORD")
```

A delivered parcel of the shop's own goods: place an order (`POST /api/orders` with an `addressId` and
`shippingOption`), wait for `Paid`, then as admin `POST /api/orders/{id}/preparing` and `POST /api/orders/{id}/shipment`
`{"trackingReference":"…"}`, then as the customer `POST /api/orders/{id}/shipments/{shipmentId}/received`. Set
`ORDER` and `SHIPMENT` from `GET /api/orders/{id}` (`shipments[0].id`), and `VARIANT` from its line
(`items[0].variantId`, or `productId` for a line from before variants).

---

## Scenario 1 - The buyer asks, once (US1, FR-001, FR-002)

```bash
curl -s -X POST http://localhost:5000/api/orders/$ORDER/shipments/$SHIPMENT/return \
  -H "Authorization: Bearer $CUSTOMER" -H 'Content-Type: application/json' \
  -d '{"reason":"The lens has a scratch across the front element"}' | jq '.status, .isShop'
# "Requested", true
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/orders/$ORDER/shipments/$SHIPMENT/return \
  -H "Authorization: Bearer $CUSTOMER" -H 'Content-Type: application/json' -d '{"reason":"again"}'
# 409
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/orders/$ORDER/shipments/$SHIPMENT/return \
  -H 'Content-Type: application/json' -d '{"reason":"x"}'
# 401
```

---

## Scenario 2 - Staff accept the shop's parcel; a customer cannot (US2)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  http://localhost:5000/api/orders/fulfilment/$ORDER/shipments/$SHIPMENT/return/accept -H "Authorization: Bearer $CUSTOMER"
# 403
curl -s -X POST http://localhost:5000/api/orders/fulfilment/$ORDER/shipments/$SHIPMENT/return/accept \
  -H "Authorization: Bearer $ADMIN" | jq .status
# "Accepted"
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/orders/returns -H "Authorization: Bearer $CUSTOMER"
# 403
```

---

## Scenario 3 - Sent back, received, refunded, restocked (US3, FR-007 to FR-010)

```bash
curl -s -X POST http://localhost:5000/api/orders/$ORDER/shipments/$SHIPMENT/return/sent \
  -H "Authorization: Bearer $CUSTOMER" -H 'Content-Type: application/json' \
  -d '{"trackingReference":"VN-RETURN-1"}' | jq .status                     # "SentBack"
curl -s http://localhost:5000/api/stock/$VARIANT | jq .quantityOnHand          # note it: N
curl -s -X POST http://localhost:5000/api/orders/fulfilment/$ORDER/shipments/$SHIPMENT/return/received \
  -H "Authorization: Bearer $ADMIN" | jq '.status, .refundAmount'             # "Received", > 0
sleep 2; curl -s http://localhost:5000/api/stock/$VARIANT | jq .quantityOnHand # N + the parcel's quantity
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  http://localhost:5000/api/orders/fulfilment/$ORDER/shipments/$SHIPMENT/return/received -H "Authorization: Bearer $ADMIN"
# 409
```

Across the three databases (what the PR shows after its Bruno run):

```bash
docker exec -it <order-db-container>     psql -U $DB_USER -d ecommerce_order_db \
  -c 'SELECT "Id","Status","RefundAmount" FROM parcel_returns ORDER BY "RequestedAt" DESC LIMIT 1;'
docker exec -it <payment-db-container>   psql -U $DB_USER -d ecommerce_payment_db \
  -c 'SELECT "Amount","Currency","Provider" FROM refunds WHERE "ReturnId" = '"'"'<return id>'"'"';'
docker exec -it <inventory-db-container> psql -U $DB_USER -d ecommerce_inventory_db \
  -c 'SELECT count(*) FROM returned_parcels WHERE "ReturnId" = '"'"'<return id>'"'"';'
```

**Expected**: `Received|3297800.00` in Order; one refund `3297800.00|VND|Stub` in Payment; one row in Inventory (the
amount is the PR's run; yours depends on the product).

---

## Scenario 4 - The staff queue (FR-015)

```bash
curl -s "http://localhost:5000/api/orders/returns?status=Received" -H "Authorization: Bearer $ADMIN" | jq '.totalCount'
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/orders/returns?status=Nope" -H "Authorization: Bearer $ADMIN"
# 400
```

---

## Scenario 5 - The money hold and the payout claim (US4, SC-003)

Time-dependent (8-day-old deliveries), so it lives in the test suite rather than in curl:

```bash
cd server
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~ReturnTests"
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~DeliveryTests|FullyQualifiedName~PayoutTests"
```

**Expected**: 24 `ReturnTests` pass, among them `An_open_return_holds_the_money_past_the_window`,
`A_final_refusal_releases_the_money`, `A_refusal_left_alone_releases_the_money_once_its_window_is_over`,
`A_returned_parcel_is_no_money_at_all` and `The_payout_claims_exactly_what_the_balance_calls_due`.

---

## Scenario 6 - Once-only refund and restock (SC-002)

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Payment.Tests   --filter "FullyQualifiedName~ReturnRefundTests"
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Inventory.Tests --filter "FullyQualifiedName~RestockReturnTests|FullyQualifiedName~AnnouncementTests"
```

**Expected**: 5 Payment tests (once however delivered, two parcels separately, never more than paid nor in another
currency, nothing for an uncharged order, not taken for the order's own refund) and 4 Inventory tests plus the
announcement pass.

---

## Scenario 7 - The whole suite and Bruno

```bash
DB_PASSWORD=<password> dotnet test            # PR: 669/669 in 9 projects
cd ../bruno && npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```
