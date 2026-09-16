# Quickstart & Validation: Order Lifecycle Visibility

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-16

Eight scenarios. Each names the requirement it proves and what a failure looks like, so that a
scenario "passing" means something specific rather than "no error appeared".

---

## Prerequisites

```bash
cd server
docker compose up -d                     # Postgres x5, RabbitMQ
./start-dev.sh                           # migrations + all 7 services
```

`.env` must carry `DB_USER`, `DB_PASSWORD`, `JWT_SECRET`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`.

> **Before anything else**: confirm nothing native is shadowing the containers. A local PostgreSQL on
> 5432 or a local RabbitMQ on 5672 wins over the container silently, and the divergence surfaces
> somewhere else entirely — this repository has lost time to both.
>
> ```bash
> docker exec ecommerce-rabbitmq rabbitmqctl status | head -3
> ```

Sign in as a shopper and keep the token:

```bash
TOKEN=$(curl -s -X POST http://localhost:5056/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"shopper@example.com","password":"..."}' \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")
```

---

## Scenario 1 — A completed order says completed *(US1, FR-001, SC-001)*

Submit an order for stock that exists, with `PAYMENT_OUTCOME=Approve`.

```bash
ORDER=$(curl -s -X POST http://localhost:5059/api/orders \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"items":[{"productId":"<id>","productName":"Keyboard","quantity":1,"unitPrice":129.99}]}' \
  | python -c "import sys,json; print(json.load(sys.stdin)['orderId'])")

sleep 5
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5059/api/orders/$ORDER
```

**Expect**: `"status": "Completed"`, `updatedAt` later than `createdAt`.

**Failure looks like**: `"Submitted"` five seconds later. Check that `OrderCompletedConsumer` is
registered and that its queue exists in the RabbitMQ management UI — a consumer that is written but
not added to `AddMassTransit` produces exactly this, with no error anywhere.

---

## Scenario 2 — A rejected payment says why *(US2, FR-002, FR-003, SC-002)*

Restart Payment with `PAYMENT_OUTCOME=Reject`, then submit another order.

**Expect**: `"status": "Failed"` and a non-empty `failureReason` naming the rejection. Then confirm
the compensation still ran:

```bash
docker exec ecommerce-inventory-db psql -U postgres -d ecommerce_inventory_db \
  -c 'SELECT "QuantityOnHand", "QuantityReserved" FROM stock_items WHERE "ProductId" = '"'"'<id>'"'"';'
```

**Expect**: `QuantityReserved` back to what it was; the units are on the shelf again.

**Failure looks like**: `Failed` with a null reason — the consumer settled the status but dropped
`Reason`. Or `Failed` with reserved units still held, which would mean this feature's consumer ran
but Inventory's release did not, and is a different bug from the one being tested.

---

## Scenario 3 — A reservation failure settles the same way *(US2 scenario 2, FR-003)*

Put `PAYMENT_OUTCOME` back to `Approve` and order more units than exist.

**Expect**: `"status": "Failed"` with the reservation's reason — the order never reached payment.

**Why this is separate from scenario 2**: the issue describes only the payment path. This is the
branch that was read out of the state machine rather than taken from the issue, and it is the one
most likely to be forgotten.

---

## Scenario 4 — Redelivery changes nothing *(US1 scenario 2, FR-004, SC-003)*

Automated, in `Ecommerce.Order.Tests`. Deliver `OrderCompletedEvent` ten times for one order.

**Expect**: one `Completed` order whose `UpdatedAt` after the tenth delivery equals its value after
the first.

**This scenario must be mutation-checked.** Remove `AND "Status" = 'Submitted'` from the guarded
update and re-run: it must fail. If it still passes, it is testing that the code runs, not that the
order is protected — which is the difference the constitution's principle V exists to enforce.

---

## Scenario 5 — A contradicting notice loses *(US1 scenario 3, FR-005)*

Automated. Settle an order `Completed`, then deliver `OrderFailedEvent` for it.

**Expect**: still `Completed`, `FailureReason` still null.

---

## Scenario 6 — An unknown order is logged, not retried *(FR-006)*

Automated, plus one manual check. Deliver `OrderCompletedEvent` for a random `Guid`.

**Expect**: no exception, no redelivery loop, and a **warning** — distinguishable from the
information-level line an already-settled order produces. Confirm the two cases do not log the same
text; if they do, the log tells an operator nothing (research D2).

---

## Scenario 7 — One shopper cannot see another's orders *(US3, FR-009, FR-010, SC-004)*

With two accounts, A and B, each holding at least one order:

```bash
curl -s -H "Authorization: Bearer $TOKEN_A" http://localhost:5059/api/orders
curl -s -o /dev/null -w '%{http_code}\n' \
  -H "Authorization: Bearer $TOKEN_A" http://localhost:5059/api/orders/$B_ORDER_ID
```

**Expect**: A's list contains only A's orders; the second call returns **404**, not 403.

**Failure looks like**: a 403. That is the disclosure FR-010 exists to prevent — it confirms the id
is real. It means ownership is being checked after the load instead of inside the query.

Also check the anonymous case:

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5059/api/orders     # expect 401
```

---

## Scenario 8 — Paging and the gateway *(FR-007, contracts/http-api.md)*

```bash
curl -s -H "Authorization: Bearer $TOKEN" 'http://localhost:5059/api/orders?page=2&pageSize=1'
curl -s -o /dev/null -w '%{http_code}\n' \
  -H "Authorization: Bearer $TOKEN" 'http://localhost:5059/api/orders?pageSize=5000'   # expect 400
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/orders             # via gateway
```

**Expect**: page 2 returns the second-newest order with an honest `totalCount`; `pageSize=5000` is a
400 with an `errors` extension rather than a silently clamped 100; and the gateway call returns the
same body as the direct call.

**The gateway line is not a formality.** `order-route` matches `/api/orders/{**catch-all}`, and
nothing in this repository has previously called the bare `/api/orders` through it. If it 404s, add
a route for the bare path — do not change the endpoint.

---

## Running the automated tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Order.Tests
```

They create and drop their own database on 5434, and bound the connection pool from the start —
feature 001's concurrency test exhausted `max_connections` and reported it as a connection error
while appearing to have passed earlier.

---

## What passing all eight does not prove

The owner filter is exercised with a substituted `ICurrentUser`, not with a real signed token — the
tests confirm the filter is applied to whatever identity is handed in, not that the right identity is
handed in at runtime. Scenario 7 covers that manually, but nothing in CI does.

That is the same shape as the role-claim incident this project has already had, where a hand-minted
token agreed with a hand-written expectation while every real token was rejected. Closing it means
asserting an order read in `.github/scripts/verify-auth.sh`, which is a task in `tasks.md` rather
than a line in this document.
