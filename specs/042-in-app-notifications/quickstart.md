# Quickstart: Validating in-app notifications

> Written on 2026-09-27, after the feature merged (#94), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md),
[messages.md](./contracts/messages.md)

How to prove the feature works. Each scenario names the requirement or success criterion it checks. The
pull request records which of them were run at the merge; see "What was run" at the end.

---

## Prerequisites

```bash
cd server
docker compose up -d                  # Postgres for every service (Activity on 5440), RabbitMQ
./start-dev.sh                        # or ./start-dev.ps1 - applies migrations, starts every service and the gateway
```

The Activity migration `20260923195702_AddNotifications` must be applied. `start-dev` does it; by hand:

```bash
dotnet ef database update --project src/Services/Activity/Ecommerce.Activity.Infrastructure/ \
                          --startup-project src/Services/Activity/Ecommerce.Activity.WebApi/
```

Tokens, through the gateway on `:5000` (the auth response carries the access token as `token`):

```bash
login() {
  curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
    -d '{"email":"'"$1"'","password":"'"$2"'"}' | jq -r .token
}
ADMIN=$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
CUSTOMER=$(login "$CUSTOMER_EMAIL" "$CUSTOMER_PASSWORD")   # a customer who has placed an order
```

The customer needs an order the saga has paid. The simplest way to get one is the Bruno collection (scenario
7), which places an order as its customer, lets the saga pay it and ships it; or place one in the storefront.

---

## Scenario 1 - A paid order tells its buyer, once (US1, SC-001)

```bash
curl -fsS "http://localhost:5000/api/notifications?page=1&pageSize=50" -H "Authorization: Bearer $CUSTOMER" \
  | jq '.items[] | {kind, data, link, readAt}'
```

**Expected**: for the order, exactly one `OrderPaid` with `data.orderId`, `data.total` and `data.currency`,
`link` `/orders/{orderId}`, `readAt` null - and, once staff or a seller ship it, one `ParcelShipped` with
`data.tracking`. No sentence anywhere in the response (FR-002).

Sellers: signed in as a seller with goods on that order, the same call shows one `NewSale` linking to
`/shop/sales/{orderId}`.

---

## Scenario 2 - The count, marking one, marking all (US2, SC-004)

```bash
curl -fsS http://localhost:5000/api/notifications/unread-count -H "Authorization: Bearer $CUSTOMER"
# {"count":2}

ID=$(curl -fsS "http://localhost:5000/api/notifications?unreadOnly=true" -H "Authorization: Bearer $CUSTOMER" | jq -r '.items[0].id')
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/notifications/$ID/read" -H "Authorization: Bearer $CUSTOMER"
# 204
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/notifications/$ID/read" -H "Authorization: Bearer $CUSTOMER"
# 204 again - read is read

curl -fsS -X POST http://localhost:5000/api/notifications/read-all -H "Authorization: Bearer $CUSTOMER"
# {"marked":1}
curl -fsS http://localhost:5000/api/notifications/unread-count -H "Authorization: Bearer $CUSTOMER"
# {"count":0}
```

**Expected**: the count drops by one after marking one, and is 0 after marking all.

---

## Scenario 3 - Nobody reads or marks another person's inbox (FR-003, SC-003)

```bash
curl -s -X POST "http://localhost:5000/api/notifications/$ID/read" -H "Authorization: Bearer $ADMIN" | jq .detail
curl -s -X POST "http://localhost:5000/api/notifications/$(uuidgen)/read" -H "Authorization: Bearer $ADMIN" | jq .detail
curl -fsS "http://localhost:5000/api/notifications?page=1&pageSize=50" -H "Authorization: Bearer $ADMIN" \
  | jq --arg id "$ID" '[.items[] | select(.id == $id)] | length'
```

**Expected**: both marks are **404**, and their `detail` is identical (outside Development the detail is
masked, so compare the status and title there). The customer's notice is not in the administrator's list
(`0`). An administrator has no special reach into an inbox.

---

## Scenario 4 - Anonymous is refused (FR-010)

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/notifications/unread-count
```

**Expected**: `401`.

---

## Scenario 5 - Paging and validation (FR-008)

```bash
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:5000/api/notifications?pageSize=51" -H "Authorization: Bearer $CUSTOMER"
curl -fsS "http://localhost:5000/api/notifications?page=1&pageSize=1" -H "Authorization: Bearer $CUSTOMER" | jq '{page, pageSize, totalCount, n: (.items|length)}'
```

**Expected**: `400` for a page size over 50; one item with the full `totalCount` for a page size of 1.

---

## Scenario 6 - The automated checks (SC-001, SC-006, research D5-D7)

Against real PostgreSQL (Activity on 5440, Order on 5434), per constitution Principle V:

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Activity.Tests --filter "FullyQualifiedName~NotificationTests"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests    --filter "FullyQualifiedName~Ecommerce.Order.Tests.NotificationTests"
```

**Expected** - every test green:

| Test | Proves |
| :-- | :-- |
| `A_notification_lands_in_its_recipient_s_inbox_once` | Four concurrent deliveries, one row (SC-006, FR-004) |
| `Nobody_reads_or_marks_another_s_notifications` | Another's list and count are empty; marking theirs and marking nothing are the same 404 (FR-003, SC-003) |
| `Marking_read_lowers_the_count_and_all_at_once_clears_it` | Count 3 → 2 → 0; marking twice is fine (SC-004) |
| `The_inbox_is_newest_first_a_page_at_a_time` | Newest first; `totalCount` over all pages |
| `A_paid_order_tells_its_buyer_and_each_seller_once` | A redelivered outcome tells nobody twice; two sellers, two `NewSale`s (FR-005, FR-006) |
| `Settling_inside_a_consumer_transaction_joins_it` | The settle joins an open transaction, the order becomes `Paid`, one `OrderPaid` (research D6) |
| `A_failed_order_tells_its_buyer_and_no_seller` | One notice only |
| `Shipping_tells_the_buyer_with_the_tracking_and_receiving_tells_the_seller` | `tracking` and `shop` on `ParcelShipped`; `ParcelReceived` to the seller |
| `A_cancellation_tells_the_buyer_and_every_seller` | `by = Customer`; the shop's own part tells nobody |
| `A_payout_tells_its_seller_how_much` | `amount` equals the payout's; link `/shop/payouts` |

The storefront:

```bash
cd client
npm test -- notification
```

**Expected**: the bell shows the unread count, loads the list only when opened, marks a chosen notice read and
navigates to it, marks all; the page asks for the unread only on its tab; the service calls name nobody;
`describeNotification` words each kind in both languages, says who cancelled in words, names "the shop" for a
parcel with no seller, and shows an unknown kind generically.

---

## Scenario 7 - Bruno (SC-002)

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

The `notifications` folder (seq 11) runs after the order folders have placed, paid and shipped an order:

| Request | Expected |
| :-- | :-- |
| the customer was told the order was paid and shipped | 200; exactly one `OrderPaid` and one `ParcelShipped` for `orderId`, each linking to the order, `readAt` null |
| the unread count | 200; at least 2 |
| marking another person notice is 404 | 404 with the admin token |
| marking a notice read | 204 |
| marking everything read | 200; `marked` is a number |
| nothing is left unread | 200; `count` is 0 |
| `security-checks/` notifications without a token is 401 | 401 |

---

## Scenario 8 - The saga still settles (SC-002, research D6)

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: passes. This is the check that could see the consumer-transaction bug: with the settle opening a
second transaction, every order stayed `Submitted` and the script reported a stall.

---

## Scenario 9 - One row per notice, in the database (FR-004)

```bash
docker exec -it ecommerce-activity-db psql -U "$DB_USER" -d ecommerce_activity_db -c \
  'SELECT "Kind", count(*), count(*) FILTER (WHERE "ReadAt" IS NULL) AS unread FROM notifications GROUP BY "Kind" ORDER BY 1;'
docker exec -it ecommerce-activity-db psql -U "$DB_USER" -d ecommerce_activity_db -c \
  "SELECT \"Id\", \"Kind\", \"Data\"->>'orderId' AS order_id, \"Link\" FROM notifications ORDER BY \"CreatedAt\" DESC LIMIT 5;"
```

**Expected**: every `Data` is a JSON object of strings; no row holds a sentence. Redelivering a message from the
RabbitMQ management UI (`http://localhost:15672`, queue prefixed `ActivitySvc`) adds no row.

---

## Scenario 10 - The words follow the reader (US3)

In the storefront (`client`, `npm run dev`), signed in as the customer: open the bell, read the notices in
English, switch the language to Tiếng Việt and open it again.

**Expected**: the same notices read as Vietnamese sentences; the unread dot and the times follow; nothing
shows a kind code or a `{{placeholder}}`. Not signed in, there is no bell.

---

## What was run

From pull request #94:

- Order tests 174/174; Activity tests 28/28.
- Client: 184 tests pass; lint, type-check and build clean.
- Bruno: 132/132 requests, 211 tests (seven requests new - scenario 7).
- `verify-saga.sh`: passes (approve branch). The reject branch was not recorded.
- Mutation checks, each red, then restored: the paid notice sent to the wrong recipient fails
  `A_paid_order_tells_its_buyer_and_each_seller_once`; the owner filter removed from mark-read fails
  `Nobody_reads_or_marks_another_s_notifications`; the join branch disabled fails the consumer-transaction test
  with the production error.
- Screenshots: the bell with 2 unread at 1360px; the notifications page at 390px with no horizontal overflow.

Scenarios 2-5, 9 and 10 as written here (curl, SQL, switching language by hand) were not recorded as run; the
same properties are covered by the tests and Bruno requests above.
