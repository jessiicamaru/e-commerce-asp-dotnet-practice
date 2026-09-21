# Observability — Following One Order

Every service and the gateway ship structured logs and distributed traces to **Seq** over OpenTelemetry
(feature 013, [specs/013-observability](../../specs/013-observability/)).

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
  is in `server/.env`. Unset, nothing is exported and nothing fails.

## The queries that matter

| Question | Seq query |
| :--- | :--- |
| Everything about one order | `OrderId = '0199…'` |
| The whole checkout, every service | open any of those events → **Trace**, or `@TraceId = '…'` |
| Saga steps only | `Transition is not null` |
| Anything that went wrong | `@Level in ['Warning', 'Error']` |
| A reply that found no saga (the 2026-09-21 stall) | `@Message like '%no saga instance%'` |

`OrderId` is added to every log line written while a service consumes a message about an order, by
`OrderIdLogScopeFilter` — handlers do not have to remember it.

## How the pieces fit

```text
client ──HTTP──▶ gateway  (new trace starts here; a client's traceparent is ignored)
                    │ traceparent
                    ▼
                 Order ──gRPC──▶ Cart / Identity / Catalog      (same trace)
                    │ OrderSubmittedEvent + traceparent in headers
                    ▼
               RabbitMQ ──▶ Orchestrator ──▶ Inventory, Payment ──▶ Order, Cart   (same trace)
```

## What is never recorded

Request and response bodies, headers (so no bearer token), and database parameter values. EF Core's
per-statement log is at Warning; the Npgsql span records each statement and its duration instead.
Changing any of that needs a reason written in [research D5](../../specs/013-observability/research.md).
