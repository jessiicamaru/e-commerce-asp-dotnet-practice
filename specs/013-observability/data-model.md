# Data Model: Following One Order Across Seven Services

> Written on 2026-09-27, after the feature merged (#33), from the code at that merge, the pull request
> and docs/guides/observability.md (this feature has no page under docs/features/).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table changed and no migration was added.** No service's database gained or lost a column. What
this feature stores lives in Seq, outside every service's database, in the compose volume `seq_data`.

## What Seq receives

Two OTLP signals from each service, over HTTP/protobuf to `${OTLP_ENDPOINT}/v1/logs` and
`${OTLP_ENDPOINT}/v1/traces`:

| Signal | Resource name | Carries |
| :--- | :--- | :--- |
| Logs | `ecommerce-<service>` (`gateway`, `identity`, `catalog`, `order`, `orchestrator`, `inventory`, `payment`, `cart`) | Level, message template, formatted message, structured properties, scopes, trace and span id |
| Traces | the same | Spans from ASP.NET Core (not `/health`), HttpClient, `MassTransit`, `Npgsql` |

## Properties a query relies on

| Property | Written by | Meaning |
| :--- | :--- | :--- |
| `OrderId` | `OrderIdLogScopeFilter<T>` (logging scope) on every consumed message with a `Guid OrderId`; the saga's `Transition` / `MissingInstance`; checkout's own log line | The order the line is about |
| `order.id` | Span tag: the same filter, and `SubmitOrderCommandHandler` on the request span | The order the span is about |
| `Transition` | `OrderStateMachine.Transition` | The saga step, e.g. `inventory reserved; requesting payment` |
| `Event` | `OrderStateMachine.MissingInstance` | The reply that found no saga instance |
| `LineCount`, `TotalAmount`, `Country`, `ShippingOption` | Checkout's `Order {OrderId} submitted: ...` line | What was ordered |

## What is never stored

Request and response bodies, headers (so no bearer token) and database parameter values (research D5).
EF Core's per-statement log (`Microsoft.EntityFrameworkCore.Database.Command`) is at Warning in every
service; the Npgsql span holds the statement text and its duration instead.

## Configuration added

| Name | Where | Meaning |
| :--- | :--- | :--- |
| `SEQ_ADMIN_PASSWORD` | `server/.env`, required by `docker-compose.yml` | Seq's first-run admin password only; Seq forces a change at the first login |
| `OTLP_ENDPOINT` | `server/.env` for host runs (`http://localhost:5341/ingest/otlp`); `docker-compose.app.yml` for containers (`http://seq:5341/ingest/otlp`) | Where to export. Unset: nothing is exported |
| `OTLP_API_KEY` | environment, optional | Sent as `X-Seq-ApiKey` when set |

Retention is Seq's default; nothing configures it (out of scope).
