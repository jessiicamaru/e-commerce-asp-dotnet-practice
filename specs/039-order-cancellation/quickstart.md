# Quickstart: Validating order cancellation

> Written on 2026-09-27, after the feature merged (#84), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Contracts**: [api.md](contracts/api.md), [messages.md](contracts/messages.md)

The effects of a cancellation live in two other services, so the scenarios that matter most look across
services - which is what `verify-saga.sh` does.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # all six services + RabbitMQ
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
CUSTOMER=...; OTHER_CUSTOMER=...; VARIANT=...   # a stocked variant of the shop's own
```

---

## Scenario 1 — The whole path, automated (US1, US2, SC-001, SC-003)

```bash
ADMIN_EMAIL=... ADMIN_PASSWORD=... SAGA_E2E_REQUIRE_ALL=1 ../.github/scripts/verify-saga.sh
```

**Expected**: after the main order, a second order is placed, paid and cancelled by its customer; the
stock returns to exactly its on-hand/reserved from before that order (the pull request's run: 47/0); a
refund of everything charged is recorded (1,155,000 of 1,155,000); cancelling again answers 200.

## Scenario 2 — By hand: stock and refund (US2)

```bash
curl -s http://localhost:5000/api/stock/$VARIANT | jq '{quantityOnHand, quantityReserved}'   # note these
# add one unit to the cart and check out, wait for Paid, then:
curl -s -X POST http://localhost:5000/api/orders/$ORDER/cancel -H "Authorization: Bearer $CUSTOMER" | jq '.status, .cancelledBy'
sleep 3
curl -s http://localhost:5000/api/stock/$VARIANT | jq '{quantityOnHand, quantityReserved}'
curl -s http://localhost:5000/api/payments/$ORDER -H "Authorization: Bearer $ADMIN" | jq '{amount, refundedAmount, refundedAt}'
```

**Expected**: `"Cancelled"`, `"Customer"`; the stock equals the first read; `refundedAmount` equals
`amount`.

```sql
-- psql localhost:5437 ecommerce_inventory_db
SELECT "Status", "SettlementReason" FROM stock_reservations WHERE "OrderId" = '<order id>';
-- psql localhost:5438 ecommerce_payment_db
SELECT count(*), sum("Amount"), min("Provider") FROM refunds WHERE "OrderId" = '<order id>';
```

**Expected**: `Released` / `Returned: order cancelled`; one refund, provider `Stub`.

## Scenario 3 — The refusals (US1.3, US1.4, FR-001, FR-002, FR-006)

```bash
curl -s -X POST http://localhost:5000/api/orders/$ORDER/cancel -H "Authorization: Bearer $OTHER_CUSTOMER" | jq .detail
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/orders/$ORDER/cancel
```

**Expected**: `Order not found.`; `401`. On an order whose shop parcel an administrator has started
preparing, the customer gets `409 This order is being prepared; ask the shop to cancel it.` - and the
administrator's `POST /api/orders/fulfilment/{id}/cancel` succeeds (US3). On an order with a shipped
parcel both get `409 Part of this order has been shipped; it can no longer be cancelled.` (Bruno's two
`admin-audit` checks.)

## Scenario 4 — Sellers and money (US3.2, SC-002)

With a cancelled order holding seller A's goods:

```bash
curl -s http://localhost:5000/api/orders/sales/$ORDER -H "Authorization: Bearer $TOKEN_A" | jq '.status, .shippingAddress'
curl -s -X POST http://localhost:5000/api/orders/sales/$ORDER/preparing -H "Authorization: Bearer $TOKEN_A" | jq .detail
curl -s -X POST http://localhost:5000/api/orders/sales/$ORDER/preparing -H "Authorization: Bearer $TOKEN_B" | jq .detail
curl -s http://localhost:5000/api/orders/payouts/due -H "Authorization: Bearer $ADMIN" | jq
```

**Expected**: `"Cancelled"`, `null`; `This order was cancelled.`; `Sale not found.` for a seller with no
part; the order's parts contribute nothing to the due list or A's balance.

## Scenario 5 — Queues (research D6)

```bash
docker exec e-commerce-rabbitmq rabbitmqctl list_queues name consumers | grep -i cancelled
```

**Expected**: `restock-cancelled-order` and `refund-cancelled-order` (MassTransit's kebab-case of the class
names), **one consumer each**.

## Scenario 6 — The automated tests (Principle V)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests     --filter "FullyQualifiedName~CancellationTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter "FullyQualifiedName~RestockTests|FullyQualifiedName~AnnouncementTests"
DB_PASSWORD=... dotnet test tests/Ecommerce.Payment.Tests   --filter "FullyQualifiedName~RefundTests"
cd ../client && npm test -- src/pages/order src/pages/admin-order src/utils/order
```

**Expected**: all pass, including `Cancel_and_ship_at_once_leave_exactly_one_winner`,
`A_cancellation_that_arrives_before_the_completion_releases_the_hold`, and
`A_delivery_that_loses_the_race_is_told_it_was_already_refunded`. At the merge: Order 152, Inventory 44,
Payment 18, client 159.
