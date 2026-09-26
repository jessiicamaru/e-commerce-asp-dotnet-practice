# Quickstart: Validating delivery confirmation

> Written on 2026-09-27, after the feature merged (#85), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/api.md](contracts/api.md) | **Data model**: [data-model.md](data-model.md)

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # or ./start-dev.sh
docker logs ecommerce-order 2>&1 | grep "Delivery confirmation sweeper started"
```

**Expected** in the log: `parcels are taken as delivered 7 day(s) after shipping, checked every
01:00:00` - what the pull request saw in the container logs.

A paid order holding the shop's goods and a seller's, with the shop's parcel shipped by an administrator
(`POST /api/orders/{id}/preparing`, then `/shipment`) and the seller's still waiting.

```bash
CUSTOMER=...; OTHER_CUSTOMER=...; SELLER=...; ADMIN=...; ORDER=...
curl -s http://localhost:5000/api/orders/$ORDER -H "Authorization: Bearer $CUSTOMER" \
  | jq '.shipments[] | {id, isShop, status, deliveredAt, deliveryConfirmedBy}'
SHOP_PARCEL=...; SELLER_PARCEL=...
```

---

## Scenario 1 — The customer confirms (US1, FR-002)

```bash
curl -s -X POST http://localhost:5000/api/orders/$ORDER/shipments/$SHOP_PARCEL/received \
  -H "Authorization: Bearer $CUSTOMER" | jq '.shipments[] | {isShop, deliveredAt, deliveryConfirmedBy}'
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/orders/$ORDER/shipments/$SHOP_PARCEL/received -H "Authorization: Bearer $CUSTOMER"
curl -s -X POST http://localhost:5000/api/orders/$ORDER/shipments/$SELLER_PARCEL/received -H "Authorization: Bearer $CUSTOMER" | jq .detail
curl -s -X POST http://localhost:5000/api/orders/$ORDER/shipments/$SHOP_PARCEL/received -H "Authorization: Bearer $OTHER_CUSTOMER" | jq .detail
```

**Expected**: the shop's parcel has `deliveredAt` and `"Customer"`, the seller's does not; the repeat is
`200`; the unshipped seller parcel is `This parcel has not been shipped yet.`; the other customer gets
`Order not found.` In the storefront the parcel reads "received on …" and the order reads "Delivered" only
when every parcel is.

## Scenario 2 — Money only for what arrived (US2, SC-001)

Have the seller prepare and ship their part, then:

```bash
curl -s http://localhost:5000/api/orders/sales/balance -H "Authorization: Bearer $SELLER" | jq
curl -s http://localhost:5000/api/orders/payouts/due -H "Authorization: Bearer $ADMIN" | jq
```

**Expected** at the time of #85: the shipped-but-unconfirmed part is **on the way** and absent from the due
list. After the customer confirms it, it is due and a payout claims it. ⚠️ Since specs/066 a delivered part
is due only once it is older than `Returns:WindowDays` (7) with no return open - so on today's stack it
stays on the way for a week after delivery; that later rule is not this feature's.

## Scenario 3 — The sweep (US3)

To see it without waiting a week, move a parcel's shipped time back and let the sweep run (restart Order,
or set `Delivery__SweepIntervalMinutes=1`):

```sql
-- psql localhost:5434 ecommerce_order_db
UPDATE order_shipments SET "ShippedAt" = now() - interval '8 days' WHERE "Id" = '<seller parcel id>';
-- after a sweep:
SELECT "Status", "ShippedAt", "DeliveredAt", "DeliveryConfirmedBy" FROM order_shipments WHERE "Id" = '<seller parcel id>';
```

**Expected**: `Shipped`, `DeliveredAt` set, `Auto`. A parcel shipped yesterday is untouched. A second sweep
changes nothing (no row has `DeliveredAt IS NULL` and an old `ShippedAt` any more).

## Scenario 4 — Order refuses a nonsensical period (US3.2)

```bash
Delivery__AutoConfirmDays=0 dotnet run --project src/Services/Order/Ecommerce.Order.WebApi/
```

**Expected**: Order does not start; the error names `Delivery:AutoConfirmDays must be at least 1.`

## Scenario 5 — End to end (SC-002)

```bash
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: passes, including "the customer confirmed the parcel arrived" (`received by Customer`) and
the specs/039 cancellation.

## Scenario 6 — The automated tests

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Order.Tests --filter "FullyQualifiedName~DeliveryTests|FullyQualifiedName~PayoutTests"
cd ../client && npm test -- src/pages/order src/pages/shop-sale src/utils/order src/components/seller/sale-earnings
```

**Expected**: the 12 `DeliveryTests` (shipping records when; confirm; not shipped; twice; someone else's;
on the way until delivered; the seller sees it received; the sweep takes only parcels shipped before the
cutoff; sweeping twice delivers once; Order does not start without a sensible period) and `PayoutTests`
pass. At the merge Order stood at 164 tests and the client at 166.
