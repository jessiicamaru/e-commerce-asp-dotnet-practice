# Quickstart: Validating the missing notices

> Written on 2026-09-27, after the feature merged (#142), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](./spec.md) | **Contracts**: [messages.md](./contracts/messages.md)

Scenario 1 and 2 are what the pull request ran (and reports passing). Whether scenarios 3 to 5 were run by hand
against a started stack is not recorded.

---

## Prerequisites

```bash
cd server
docker compose up -d
./start-dev.sh          # every service and the gateway on :5000
cd ../client && npm install
```

An administrator token as `$ADMIN` (see [specs/058 quickstart](../058-audit-gaps/quickstart.md#prerequisites)), and
a registered customer with token `$CUSTOMER` and id `$USER_ID`.

---

## Scenario 1 - The server tests (SC-001)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests \
  --filter "FullyQualifiedName~ModerationTests.Every_action_is_on_the_record_with_its_diff_and_a_grant_tells_the_person"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests \
  --filter "FullyQualifiedName~NotificationTests.The_sweep_taking_a_parcel_as_delivered_tells_its_seller"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests \
  --filter "FullyQualifiedName~ReviewTests.A_hidden_review_is_neither_shown_nor_counted"
```

**Expected**: all pass. Each asserts one notice to the right recipient with the declared keys; the Order test
also asserts that no `ParcelReceived` was sent. At the merge the whole suites were Order 184/184, Identity 83/83,
Catalog 161/161.

---

## Scenario 2 - The storefront's words (SC-002, SC-003)

```bash
cd client
npm test -- src/utils/notifications
```

**Expected**: passes, including "words a lock with its end in the reader language and the reason" and the
per-kind cases under "every kind a service can send (specs/048)". Before the change the latter were 9 red: one per
missing sentence (four kinds, two languages) and one for the kinds list. At the merge the full storefront suite
was 291/291, with `npm run lint` and `npx tsc -b` clean.

---

## Scenario 3 - A lock and a ban tell the person (FR-001, FR-002)

```bash
curl -fsS -X POST "http://localhost:5000/api/users/$USER_ID/lock" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"days":2,"reason":"Cooling off"}'
curl -fsS -X POST "http://localhost:5000/api/users/$USER_ID/unlock" -H "Authorization: Bearer $ADMIN"
# sign in again as the customer to get a fresh $CUSTOMER, then:
curl -fsS "http://localhost:5000/api/notifications" -H "Authorization: Bearer $CUSTOMER" | jq '.items[0] | {kind, data}'
```

**Expected**: `{"kind":"AccountLocked","data":{"until":"<ISO UTC>","reason":"Cooling off"}}`. In the storefront's
bell the end shows as a local date and time in the chosen language. A ban (`POST /api/users/{id}/ban` with
`{"reason":"Fraud"}`) leaves an `AccountBanned` notice the same way, readable once the ban is lifted.

---

## Scenario 4 - A hidden review tells its author (FR-003)

With a review id `$REVIEW` written by the customer:

```bash
curl -fsS -X POST "http://localhost:5000/api/reviews/$REVIEW/hide" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"Advertising"}'
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/reviews/$REVIEW/hide" \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"reason":"Again"}'
```

**Expected**: the author has one `ReviewHidden` notice with `product` and `reason`, linked to `/products/{id}`;
the second hide is `409` and sends nothing.

---

## Scenario 5 - The sweep tells the seller (FR-004)

Ship a seller's parcel, then move it more than 7 days back in Order's database and wait for the next sweep
(`Delivery:SweepIntervalMinutes`, 60 by default - lower it for the test):

```sql
-- Order, localhost:5434, ecommerce_order_db
UPDATE order_shipments SET "ShippedAt" = now() - interval '8 days'
 WHERE "OrderId" = '<order id>' AND "SellerId" = '<seller id>';
```

**Expected**: after the sweep, the part has `DeliveredAt` set with `DeliveryConfirmedBy` = Auto, the seller has one
`ParcelAutoDelivered` notice linked to `/shop/sales/{orderId}`, and no `ParcelReceived`. The shop's own part, swept
the same way, tells nobody.
