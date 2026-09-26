# Quickstart: Validating per-seller shipments

> Written on 2026-09-27, after the feature merged (#79), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/api.md](contracts/api.md) | **Data model**: [data-model.md](data-model.md)

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
```

You need an order holding goods from **two sellers** (A and B), paid, and a second order holding the
**shop's** goods beside one seller's. The pull request used the demo data (Minh's order: Mai's lens +
Tuấn's card and battery; An's order: the shop's Ricoh + Tuấn's card). With fresh accounts: two sellers
list, get approved and stock a product each (specs/031, 044, 045), a customer puts both in the cart and
checks out, and waits for `Paid`.

```bash
ORDER=...; TOKEN_A=...; TOKEN_B=...; TOKEN_OTHER=...   # a third seller with nothing on ORDER
CUSTOMER=...; ADMIN=...
```

---

## Scenario 1 — A seller moves only their own part (US1, SC-001)

```bash
curl -s -X POST http://localhost:5000/api/orders/sales/$ORDER/preparing -H "Authorization: Bearer $TOKEN_A" | jq .status
curl -s -X POST http://localhost:5000/api/orders/sales/$ORDER/shipment -H "Authorization: Bearer $TOKEN_A" \
  -H 'Content-Type: application/json' -d '{"trackingReference":"VNPOST-1"}' | jq '.status, .trackingReference, .shippingAddress'
curl -s -X POST http://localhost:5000/api/orders/sales/$ORDER/preparing -H "Authorization: Bearer $TOKEN_OTHER" | jq .detail
curl -s -X POST http://localhost:5000/api/orders/sales/$(uuidgen)/preparing -H "Authorization: Bearer $TOKEN_A" | jq .detail
```

**Expected**: `"Preparing"`; then `"Shipped"`, `"VNPOST-1"`, and `shippingAddress: null` (US2.2); then
`Sale not found.` twice - a seller with no part on the order and a made-up order read the same.

## Scenario 2 — Repeats and wrong steps (US1.2)

Repeat the ship with `VNPOST-1`: **200**, nothing changed. With `VNPOST-2`: **409** `Your part is already
Shipped with tracking reference 'VNPOST-1'.` As seller B, ship before preparing: **409** `Your part is Paid;
only a Preparing part can become Shipped.` Ship with an empty reference: **400**.

## Scenario 3 — The address, while it is the job (US2, SC-003)

```bash
curl -s http://localhost:5000/api/orders/sales/$ORDER -H "Authorization: Bearer $TOKEN_B" | jq '.status, .shippingAddress'
```

**Expected** (B has not shipped): the address and phone are present; no customer id or email anywhere in
the body. After B ships, the same read gives `shippingAddress: null`.

## Scenario 4 — The customer sees the parcels (US3)

```bash
curl -s http://localhost:5000/api/orders/$ORDER -H "Authorization: Bearer $CUSTOMER" | jq '.status, .trackingReference, .shipments'
curl -s http://localhost:5000/api/orders -H "Authorization: Bearer $CUSTOMER" | jq '.items[] | select(.id=="'$ORDER'") | {status, shipmentCount, shipmentsShipped}'
```

**Expected** with A shipped and B not: order `Preparing`, `trackingReference: null`, two `shipments`
(one `Shipped` with `VNPOST-1`, one `Paid` or `Preparing`), and `shipmentCount: 2, shipmentsShipped: 1`.
When B ships too, the order reads `Shipped` (SC-002). The list is a `PagedResponse` (`items`, `page`,
`pageSize`, `totalCount`); an older order may be on a later page.

## Scenario 5 — The shop's part (US4)

```bash
curl -s -X POST http://localhost:5000/api/orders/$ORDER/preparing -H "Authorization: Bearer $ADMIN" | jq .detail
```

**Expected** on the two-seller order: **409** `This order has no part the shop ships; each seller ships
their own.` On the mixed order: the administrator's prepare and ship move only the shop's parcel; after
the seller's part and the shop's are both shipped the order is `Shipped` and carries no tracking of its
own, each parcel keeping its own - as the pull request's run on An's order showed. A customer calling a
seller route gets **403**, anonymous **401** (Bruno `security-checks`).

## Scenario 6 — One parcel behaves as before (SC-004)

```bash
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: passes unchanged. The Bruno admin flow (shop goods only) also passes unchanged.

## Scenario 7 — Parts exist for every order (FR-011, data model)

```sql
-- psql on localhost:5434, ecommerce_order_db
SELECT count(*) FROM orders o WHERE NOT EXISTS (SELECT 1 FROM order_shipments s WHERE s."OrderId" = o."Id");
SELECT "OrderId", "SellerId", count(*) FROM order_shipments GROUP BY 1, 2 HAVING count(*) > 1;
```

**Expected**: `0` (what the pull request recorded after the backfill) - unless an older image has written
orders since, which get their parts at their first move - and no rows from the second query.

## Scenario 8 — The automated tests and the concurrency case (FR-010, Principle V)

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~ShipmentTests|FullyQualifiedName~FulfilmentTests|FullyQualifiedName~SellerSalesTests"
cd ../client && npm test -- src/pages/shop-sale src/components/order/order-shipments
```

**Expected**: all pass, including `Two_parts_shipped_at_the_same_moment_leave_the_order_shipped` and
`Parts_missing_from_an_older_order_are_made_in_the_order_s_state`. At the merge Order stood at 98 tests
and the client at 104.
