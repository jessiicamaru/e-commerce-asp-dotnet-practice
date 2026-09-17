# Quickstart & Validation: One Source of Truth for Stock

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-17

Eight scenarios. Each names the requirement it proves and what a failure looks like, so "it passed"
means something specific.

---

## Prerequisites

```bash
cd server
docker compose up -d
./start-dev.sh
```

`.env` needs `DB_USER`, `DB_PASSWORD`, `JWT_SECRET`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`.

> **Check nothing native is shadowing the containers first.** A local PostgreSQL on 5432 or a local
> RabbitMQ on 5672 wins over the container silently, and this repository has lost time to both.
>
> ```bash
> docker exec e-commerce-rabbitmq rabbitmqctl status | head -3
> ```

> **Then check the queues, before anything else.** Catalog is gaining its first consumer, and
> feature 003 shipped a defect where two services' consumer classes collided on one queue and
> competed for it — the order settled and the stock stayed held, with all fifteen unit tests green.
>
> ```bash
> docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages consumers
> ```
>
> Every queue should show **1** consumer. `CatalogSvcStockAvailabilityChanged` should appear.

```bash
ADMIN=$(curl -s -X POST http://localhost:5056/api/auth/login \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")
```

---

## Scenario 1 — A new product reads out of stock, honestly *(US3, FR-005, SC-007)*

Create a product. Do not stock it.

```bash
curl -s -X POST http://localhost:5057/api/products \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"name":"Quickstart Widget","description":null,"price":9.99,"sku":"QS-001","categoryId":"<id>"}'
```

**Expect**: `"availability": "OutOfStock"`, and **no `stockQuantity` field anywhere in the response**.

**Failure looks like**: `"OutOfStock"` is right, but a `stockQuantity` still present means the field
was added alongside rather than replacing — the defect with a correct field next to it.

Also confirm the field is rejected rather than ignored:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5057/api/products \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"name":"X","price":1,"sku":"QS-002","categoryId":"<id>","stockQuantity":50}'
```

**Expect**: a 4xx. A 200 means an administrator can believe they set stock when they did not.

---

## Scenario 2 — Stocking a product flips the listing *(US2, FR-004, SC-003)*

```bash
curl -s -X PUT http://localhost:5060/api/stock/<productId> \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"quantityOnHand":2}'

sleep 3
curl -s http://localhost:5057/api/products/<productId>
```

**Expect**: `"availability": "InStock"` within 10 seconds, with no manual step in between.

**Failure looks like**: still `"OutOfStock"`. Check that `SetStockOnHandCommandHandler` publishes,
and that `CatalogSvcStockAvailabilityChanged` exists with one consumer. A handler that was written
but whose publish call was never added produces exactly this, with no error anywhere.

---

## Scenario 3 — Buying the last unit flips it back *(US2, FR-011, SC-001, SC-002)*

Order both units as a shopper, let the saga complete, then read the listing.

**Expect**: `"availability": "OutOfStock"`, and Inventory's own figure agrees:

```bash
curl -s http://localhost:5060/api/stock/<productId>   # quantityAvailable: 0
curl -s http://localhost:5057/api/products/<productId> # "OutOfStock"
```

**This is the scenario the whole feature is for.** Before this change the listing would still have
reported the number typed at creation.

**Failure looks like**: the two disagree. Note *which* — a listing saying `InStock` when Inventory
says 0 is the dangerous direction and means an announcement was lost or overtaken.

---

## Scenario 4 — Held units do not count as available *(FR-011, US2 scenario 3)*

Stock a product to 1. Submit an order for it but set `PAYMENT_OUTCOME=Reject` **in `server/.env`**
so the hold lingers before compensating.

> `PAYMENT_OUTCOME=Reject dotnet run ...` does not work — every `Program.cs` loads `.env` by calling
> `Environment.SetEnvironmentVariable`, which *overwrites* what you exported. Edit `.env`, restart
> Payment, and put it back. `/health` reports `configuredOutcome`; check it took.

**Expect**: while the unit is held, the listing reads `"OutOfStock"` — availability is what a *new*
shopper could buy. After the compensation releases it, the listing returns to `"InStock"`.

---

## Scenario 5 — Redelivery changes nothing *(FR-006, SC-004)*

Automated, in `Ecommerce.Catalog.Tests`. Deliver one announcement ten times.

**Expect**: one row, and `AvailabilityObservedAt` identical after the tenth delivery and the first.

**Mutation-check this.** Remove the guard from the update and re-run: it must fail. A test that
passes with and without the guarantee is not testing it.

---

## Scenario 6 — An overtaken announcement loses *(FR-007, SC-005)*

Automated. Deliver `unavailable @10:00:07` first, then `available @10:00:05`.

**Expect**: the product still reads unavailable. The **later observation** wins, not the later
delivery.

**Mutation-check this separately.** Remove only `AND ("AvailabilityObservedAt" IS NULL OR ... < @observedAt)`
and keep the rest: scenario 5 will still pass and this one must fail. That separation is the point —
the two requirements look like one and are not.

---

## Scenario 7 — Every path that moves stock announces *(FR-004, the real risk)*

Automated, in `Ecommerce.Inventory.Tests`. One test per handler:

| Handler | Exercised by |
| :--- | :--- |
| `RegisterProduct` | registering a product announces `IsAvailable = false` |
| `SetStockOnHand` | setting a level announces the new availability |
| `ReserveStock` | reserving the last units announces `false` |
| `ReleaseStock` | releasing a hold announces `true` |
| `ConfirmStock` | confirming announces the level after the units leave |
| `ExpireStock` | the sweeper returning a hold announces `true` |

**These six are the most valuable tests in the feature.** Six publish sites is six chances to forget
one, and a forgotten one is invisible — the listing simply stops updating for that path until a
shopper buys something that is gone.

**Failure looks like**: five pass, one does not. That is the test doing its job.

---

## Scenario 8 — An announcement for an unknown product *(FR-008)*

Automated, plus one manual check. Deliver an announcement for a random `Guid`.

**Expect**: no exception, no redelivery loop, and a **`Warning`** — distinguishable from the
`Information` line an already-current product produces. If the two log the same text, an operator
reading one learns nothing.

---

## Running the automated tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test
```

`Ecommerce.Catalog.Tests` creates and drops its own database on 5433 and bounds its connection pool
from the start — feature 001's concurrency test exhausted `max_connections` and reported it as a
connection error while appearing to pass on an earlier run.

---

## After deploying: the whole catalogue reads out of stock

Expected, and not a failure. The migration defaults `Availability` to `false`, and Catalog genuinely
does not know anything about existing products until Inventory announces. No backfill is attempted —
Catalog cannot read Inventory's database (constitution I), and asking over HTTP at migration time
would make a schema change depend on a service being awake.

The fix is an announcement. Either wait for the next stock movement, or force one per product:

```bash
curl -s -X PUT http://localhost:5060/api/stock/<productId> \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' \
  -d '{"quantityOnHand":<its current onHand>}'
```

---

## What passing all eight does not prove

**Nothing in CI runs Catalog and Inventory together against a real broker.** The unit tests cover
each side against a real database — the six publish sites, and the consumer's ordering and
idempotency — but the announcement actually travelling between the two services is exercised only by
hand, in scenarios 1–4.

That is the same gap feature 003 left open, and it is exactly the gap that let feature 003's queue
collision through: fifteen green tests, and a defect that only appeared when the thing was run. It
belongs to the roadmap's Phase 7, and it is named here rather than discovered later.
