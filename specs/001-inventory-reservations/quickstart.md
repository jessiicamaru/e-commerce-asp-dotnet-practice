# Quickstart: Validating Inventory Reservations

**Feature**: [spec.md](./spec.md) | **Contracts**: [messages.md](./contracts/messages.md), [http-api.md](./contracts/http-api.md)

How to prove the feature works once it is built. Each scenario maps to a success criterion, so a
failure here is a failure of the spec, not of a test's opinion.

---

## Prerequisites

```bash
cd server
docker compose up -d                 # includes the new postgres-inventory on 5437
dotnet ef database update --project src/Services/Inventory/Ecommerce.Inventory.Infrastructure/ \
                          --startup-project src/Services/Inventory/Ecommerce.Inventory.WebApi/
./start-dev.ps1                      # Identity 5056, Catalog 5057, Orchestrator 5058, Order 5059, Inventory 5060
```

`server/.env` needs `INVENTORY_DB_PORT=5437` alongside the existing entries. A locally installed
PostgreSQL or RabbitMQ will shadow the containers — see
[troubleshooting §6](../../docs/guides/troubleshooting.md) if anything behaves oddly.

Get an admin token (the seeded administrator from `ADMIN_EMAIL` / `ADMIN_PASSWORD`):

```bash
TOKEN=$(curl -fsS -X POST http://localhost:5056/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)
```

---

## Scenario 1 — A product registers itself, then gets stocked (FR-014, FR-015)

Create a product in Catalog, then confirm inventory picked it up at zero and can be stocked.

1. `POST /api/products` on Catalog as admin.
2. `GET /api/stock/{productId}` on Inventory → `200`, all quantities `0`. Proves the
   `ProductCreatedEvent` consumer registered it.
3. `PUT /api/stock/{productId}` with `{"quantityOnHand": 10}` as admin → `200`, available `10`.

**Expected**: the product exists in inventory without anyone registering it by hand, and starts
unsellable until stocked.

---

## Scenario 2 — An order reserves stock and the saga moves on (User Story 1, SC-001, SC-002)

1. Submit an order for 3 units as a customer: `POST /api/orders`.
2. `GET /api/stock/{productId}` → on hand `10`, reserved `3`, available `7`.
3. `GET /api/reservations/{orderId}` as admin → one entry, `Held`, with an `expiresAt`.
4. Check the order left `Submitted`.

**Expected**: within about a second. This is the criterion the whole feature exists for — before
it, step 4 never happened at all.

> The saga stops at `InventoryReservedState` because no payment service exists yet. That is
> correct for now. Continue to scenario 5 by publishing the payment event by hand.

---

## Scenario 3 — An impossible order is refused, cleanly (User Story 2, SC-003)

1. Submit an order for 999 units of a product with 7 available.
2. `GET /api/stock/{productId}` → unchanged: reserved still `3`, available still `7`.
3. The order reaches a failed state, with a reason naming the product.

Repeat with an order containing one available and one unavailable line, and confirm **neither** is
reserved — all-or-nothing (FR-003).

---

## Scenario 4 — Payment fails and the stock comes back (User Story 3, SC-006)

With a reservation held, publish `PaymentFailedEvent` for that order (RabbitMQ management UI at
`http://localhost:15672`, or a small publisher).

**Expected**: the saga sends `ReleaseInventoryCommand`, available returns to `10`, and the
reservation reads `Released` with the failure reason.

Publish the same event again: quantities do **not** change. That is FR-006 and User Story 3
scenario 3.

---

## Scenario 5 — A completed order deducts permanently (D2)

With a reservation held, publish `PaymentProcessedEvent` for that order.

**Expected**: the saga publishes `OrderCompletedEvent`; inventory confirms. On hand drops from `10`
to `7`, reserved returns to `0`, available stays `7`. The reservation reads `Confirmed`.

The distinction from scenario 4 is the point: released stock returns to the shelf, confirmed stock
leaves the building.

---

## Scenario 6 — Abandoned checkout recovers on its own (User Story 4, SC-007)

1. Set the holding period short for the test (`INVENTORY_RESERVATION_TTL_MINUTES=1`) and restart
   Inventory.
2. Submit an order, confirm units are `Held`, then send neither payment event.
3. Wait out the holding period plus one sweep interval.

**Expected**: available returns to its original level, the reservation reads `Expired`. Then
publish a late `PaymentFailedEvent` for that order and confirm quantities do **not** change again
(FR-017).

---

## Scenario 7 — No overselling under concurrency (SC-004)

The criterion that cannot be checked by hand, and the one most worth automating.

Stock a product with exactly 10 units, fire 100 concurrent single-unit orders, wait for the dust to
settle, then assert:

- exactly 10 reservations are `Held` (or settled), and 90 orders failed;
- `quantityReserved` is exactly `10` and never exceeded it at any point;
- no order is left without an outcome (SC-001).

This belongs in the test project rather than in a shell script — see
[research.md D7](./research.md). Run against a real PostgreSQL: the guarantee under test *is* the
database's row locking, so an in-memory provider would pass against broken code.

---

## Scenario 8 — Replay changes nothing (SC-005)

Deliver the same `ReserveInventoryCommand` twice (redeliver from the management UI, or call the
consumer twice in the harness).

**Expected**: one reservation row, one reply, stock moved once. Repeat for
`ReleaseInventoryCommand` and `ProductCreatedEvent`.

---

## At-rest consistency check (SC-008)

With nothing in flight, every product satisfies `quantityReserved == 0` and
`quantityAvailable == quantityOnHand`. Worth asserting at the end of any test run — a violation
means a transition leaked.
