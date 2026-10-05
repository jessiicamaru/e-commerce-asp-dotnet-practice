# Observability — Following One Order

Every service and the gateway ship structured logs and distributed traces to **Seq** over OpenTelemetry
(feature 013, [specs/013-observability](../../specs/013-observability/)) - nine processes: the gateway,
Identity, Catalog, Cart, Order, Inventory, Payment, the Orchestrator and Activity. Each calls
`AddObservability("<name>")` from `Ecommerce.Shared/Observability`, so a service's events carry its
name as the OpenTelemetry service name (`ecommerce-order`, `ecommerce-activity`, ...).

Seq holds **operational** logs. The record of who changed what - the audit log - is a different thing,
kept by the Activity service in its own database and read at `/api/audit` (specs/041); it is not
derived from these logs, and nothing here replaces it.

## Start it

```bash
cd server
# server/.env needs SEQ_ADMIN_PASSWORD (see .env.example) - compose refuses to start Seq without it
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

- UI: <http://localhost:5380>, user `admin`. **The first login asks you to choose a new password** —
  `SEQ_ADMIN_PASSWORD` is only the initial one. Keep `.env` in step if scripts use it.
- Containers export automatically (`OTLP_ENDPOINT` is set in `docker-compose.app.yml`).
- Services on the host (`start-dev`) export only if `OTLP_ENDPOINT=http://localhost:5341/ingest/otlp`
  is in `server/.env`. Unset, nothing is exported and nothing fails. Seq itself is part of
  `docker-compose.yml`, so `docker compose up -d` starts it on the host path too.

## The queries that matter

| Question | Seq query |
| :--- | :--- |
| Everything about one order | `OrderId = '0199…'` |
| The whole checkout, every service | open any of those events → **Trace**, or `@TraceId = '…'` |
| Saga steps only | `Transition is not null` |
| Anything that went wrong | `@Level in ['Warning', 'Error']` |
| A reply that found no saga (the 2026-09-21 stall) | `@Message like '%no saga instance%'` |

`OrderId` is added to every log line written while a service consumes a message about an order, by
`OrderIdLogScopeFilter` — handlers do not have to remember it. The filter is registered on every
service that consumes messages (Catalog, Cart, Order, Inventory, Payment, the Orchestrator and
Activity); Identity consumes none.

## How the pieces fit

```text
client ──HTTP──▶ gateway  (new trace starts here; a client's traceparent is ignored)
                    │ traceparent
                    ▼
                 Order ──gRPC──▶ Cart / Identity / Catalog      (same trace)
                    │ OrderSubmittedEvent + traceparent in headers
                    ▼
               RabbitMQ ──▶ Orchestrator ──▶ Inventory, Payment ──▶ Order, Cart   (same trace)
                    │
                    └──▶ Activity (the audit entries and notifications each step published)
```

The same propagation covers the other synchronous edges - Cart asking Catalog to describe a cart,
Inventory asking Catalog who owns a variant - and every other message, such as `OrderCancelledEvent`
reaching Inventory and Payment.

## What is never recorded

Request and response bodies, headers (so no bearer token), and database parameter values. EF Core's
per-statement log is at Warning; the Npgsql span records each statement and its duration instead.
Changing any of that needs a reason written in [research D5](../../specs/013-observability/research.md).

## Metrics: Prometheus and Grafana (specs/148)

Seq answers "what happened to this order". Metrics answer "how is the shop doing right now": how many orders settle a
minute, how long they take, what is waiting in each outbox and in each queue. Design record:
[specs/148-metrics](../../specs/148-metrics/).

```bash
cd server
# server/.env needs GRAFANA_ADMIN_PASSWORD (see .env.example) - compose refuses to start Grafana without it
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

- **Grafana**: <http://localhost:3000>, user `admin`. Its home page is the **E-commerce overview** dashboard.
- **Prometheus**: <http://localhost:9090>, for ad-hoc queries.

### How a number gets there

```text
service ──OTLP push every 15 s──▶ Prometheus (/api/v1/otlp, --web.enable-otlp-receiver) ◀──scrape── RabbitMQ :15692
                                        ▲
                                     Grafana (datasource and dashboard provisioned from server/observability/grafana)
```

- Each service pushes when `METRICS_ENDPOINT` is set (`docker-compose.app.yml` sets it; unset, nothing is registered and
  nothing fails - research D6). A service on the host exports only with
  `METRICS_ENDPOINT=http://localhost:9090/api/v1/otlp` in `server/.env`.
- Pushing rather than a `/metrics` endpoint per service means no new port, no scrape list to keep in step with the
  services, and nothing more for the gateway to keep private (research D1).
- What every service sends: ASP.NET Core's request durations, HttpClient, the .NET runtime (memory, GC, threads),
  MassTransit's consume/publish counts and durations, Npgsql's connection pool, and the shop's own meter, `Ecommerce`.
- RabbitMQ is scraped through the `rabbitmq_prometheus` plugin its management image already runs; per-queue depths come
  from `/metrics/detailed` (research D4).

### The shop's own numbers are read from committed rows

| Metric | Labels | From |
| :-- | :-- | :-- |
| `ecommerce_orders` | `status` | Order: orders per status, counted in the database |
| `ecommerce_order_settle_seconds` | `quantile` (0.5, 0.95, 0.99) | Order: `CreatedAt` → `PaidAt` of the orders paid in the last minute |
| `ecommerce_outbox_pending_messages` | `job` | every service with an outbox: messages committed and not yet delivered to the broker |

⚠️ **These are gauges sampled every 15 s (`METRICS_SAMPLE_SECONDS`), never counters incremented in a handler.** A
consume runs in a transaction that can roll back and be retried (specs/145): a counter bumped inside it would count the
order twice, and one bumped after the commit is lost when the process stops in between. A query of committed rows can
be neither (research D2). "Orders settled per minute" is therefore `delta(ecommerce_orders[1m])`. A sample that throws
is logged and the gauge keeps its previous reading, so a database blip draws a flat line rather than a drop to zero.
A new number about the shop is added the same way: `services.AddSampledGauge(name, unit, description, sample)` from
`Ecommerce.Shared/Observability`.

### The dashboard

Four rows - **Checkout** (paid, failed, waiting; settled per minute; settle percentiles; each outbox's backlog),
**Services** (requests a second, p95, 5xx - health probes excluded), **Messaging** (consumed a second by message type,
consume failures by exception, the ten deepest queues, consume p95) and **Runtime** (memory, connections, GC pause).

It is provisioned read-only from `server/observability/grafana/dashboards/ecommerce.json`, which
`server/observability/grafana/generate_dashboard.py` writes: a change is made there, regenerated and reviewed like
code, not clicked into one Grafana and lost with its volume (research D5). Every panel's query was run against
Prometheus during the broker-fault run and returned data.

| Question | Look at |
| :-- | :-- |
| Are orders getting stuck? | **Orders waiting** not returning to zero; settle p99 climbing |
| Is the broker down or behind? | **Waiting in each outbox** rising; queue depths rising |
| Is something faulting? | **Consume failures** - most are retried conflicts (specs/145); a faulted one appears as an `_error` queue in the queue panel |
| Which service is slow? | **Latency p95** per service, then Seq for the traces |
