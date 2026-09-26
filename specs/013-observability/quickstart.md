# Quickstart: Following One Order Across Seven Services

> Written on 2026-09-27, after the feature merged (#33), from the code at that merge, the pull request
> and docs/guides/observability.md (this feature has no page under docs/features/).

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md)

The results quoted are the ones the pull request recorded, from a run against the containers with Seq
up.

---

## Prerequisites

```bash
cd server
# server/.env needs SEQ_ADMIN_PASSWORD (compose refuses to start Seq without it), plus the usual
# DB_USER, DB_PASSWORD, RABBITMQ_*, ADMIN_EMAIL, ADMIN_PASSWORD
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

Open <http://localhost:5380>, sign in as `admin` with `SEQ_ADMIN_PASSWORD`, and choose a new password
when Seq asks - it always does at the first login. The containers export on their own
(`OTLP_ENDPOINT=http://seq:5341/ingest/otlp`); services started with `start-dev` export only if
`OTLP_ENDPOINT=http://localhost:5341/ingest/otlp` is in `server/.env`.

---

## Scenario 1 - One order, one query, one trace (US1, SC-001)

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh    # places a real order
```

In Seq: `OrderId = '<the order id the script prints>'`, then open any returned event → **Trace**.

**Expected**: lines from Order, the Orchestrator, Inventory and Payment, all on one trace id; the trace
spans the gateway, Cart, Catalog, Identity, Inventory, the Orchestrator, Order and Payment. The pull
request recorded **8 lines** (order 2, orchestrator 3, inventory 2, payment 1) and one trace across
cart, catalog, identity, inventory, orchestrator, order and payment.

## Scenario 2 - The saga's steps (FR-005)

In Seq: `Transition is not null`.

**Expected**: one Information event per saga move, e.g. `Saga <id>: inventory reserved; requesting
payment`, each with `OrderId`.

## Scenario 3 - An orphaned reply is a Warning (US2, SC-002)

Publish an `InventoryReservedEvent` for an order id no saga instance holds: in the RabbitMQ management
UI (<http://localhost:15672>) → Exchanges → `Ecommerce.Contracts.Inventory:InventoryReservedEvent` →
Publish message, with the MassTransit envelope as the payload:

```json
{ "messageType": ["urn:message:Ecommerce.Contracts.Inventory:InventoryReservedEvent"],
  "message": { "orderId": "<a fresh guid>", "reservedAt": "2026-09-22T00:00:00Z" } }
```

In Seq: `@Message like '%no saga instance%'`.

**Expected**, at the default level, with no restart:

```text
WARN  Saga <id>: InventoryReservedEvent arrived but no saga instance exists for it, so it was
      discarded ...
```

The pull request recorded exactly that. Whether it used the management UI or another publisher is not
recorded.

## Scenario 4 - Nothing secret (US3, SC-003)

After scenario 1 and a run of `verify-auth.sh`, search Seq for `@Message like '%eyJ%'` and look at the
properties of a few request spans.

**Expected**: 0 events containing a JWT-looking string; no `Authorization` header on any span; Npgsql
spans show statement text only. The pull request recorded **0**.

## Scenario 5 - A client cannot choose the trace (FR-006)

```bash
curl -s -o /dev/null -H 'traceparent: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01' \
  http://localhost:5000/api/products
```

In Seq: `@TraceId = '0af7651916cd43dd8448eb211c80319c'`.

**Expected**: nothing - the gateway started its own trace. Whether this was run for the pull request is
not recorded; the behaviour is the propagator's (`ExtractTraceIdAndState` returns no id).

## Scenario 6 - No collector, no change (FR-008, SC-004)

Run the services without `OTLP_ENDPOINT` (unset it in `server/.env` and use `start-dev`), then:

```bash
DB_PASSWORD=... dotnet test
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: everything passes and nothing is exported. CI runs this way on every pull request - it
has no Seq and sets no `OTLP_ENDPOINT`. The pull request recorded `dotnet test` 113/113 and both
scripts passing **with telemetry on**.

## Scenario 7 - Less noise (D5)

Compare the `OrderId` query of scenario 1 with EF's command logging raised back to Information: set
`Microsoft.EntityFrameworkCore.Database.Command` to `Information` under `Logging:LogLevel` in each
service's `appsettings.json`, rebuild, and place another order.

**Expected**: many more lines. The pull request recorded 151 lines for one order before the level was
lowered and 8 after.
