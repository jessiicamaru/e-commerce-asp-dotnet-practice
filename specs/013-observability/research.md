# Research: Following One Order Across Seven Services

## D1 — OpenTelemetry, not Serilog

**Decision**: `Microsoft.Extensions.Logging` + OpenTelemetry (logs and traces), OTLP over HTTP/protobuf
to Seq's `/ingest/otlp/v1/{logs,traces}`. Packages 1.19 in `Ecommerce.Shared`, wired by one call,
`builder.AddObservability("<service>")`.

**Rejected**: Serilog + Seq sink (the roadmap's original plan) — logs only, one vendor's pipeline, and
the stall was a *latency* finding that a trace shows directly. Seq ingests OTLP, so nothing is lost.

## D2 — What produces spans

ASP.NET Core (server, including gRPC), HttpClient (so every gRPC client call), MassTransit
(`ActivitySource "MassTransit"`: send, publish, consume, saga) and Npgsql (one span per command,
statement text only). Health probes are filtered out — Docker calls them every few seconds.

## D3 — Where the trace starts

**Decision**: the gateway registers a `DistributedContextPropagator` that **ignores** incoming trace
context and injects normally. ASP.NET Core's hosting layer takes the propagator from DI, so the
gateway's request activity is always a new root.

**Rejected**: accepting `traceparent` from callers — convenient, and it lets any caller pick a trace id,
collide two checkouts into one trace, or attach to someone else's.

## D4 — `OrderId` without touching every handler

A MassTransit consume filter (`OrderIdLogScopeFilter<T>`) reads a `Guid OrderId` property off the
message (reflected once per type), opens a logging scope with it and tags the span `order.id`.
Registered once per bus with `cfg.UseConsumeFilter(typeof(OrderIdLogScopeFilter<>), context)`. Checkout
itself logs the order id when it creates it.

## D5 — Secrets

Nothing used here records headers or bodies by default, and nothing turns that on. EF Core logs
parameters as `'?'` unless sensitive-data logging is enabled; it is not. EF's per-command Information
logging is lowered to Warning in every service — it made up most of an order's log lines and the
Npgsql span already carries each statement and its duration.

## D6 — Seq's admin password

Seq 2024 refuses to start with an empty first-run password, and forces a change at the first login.
`SEQ_ADMIN_PASSWORD` is therefore required by `docker-compose.yml` (`${VAR:?}`); `.env.example`
documents that the value is only the initial one.

## D7 — Proving the stall is now visible

Reverting the orchestrator's outbox on a scratch build did **not** reproduce the race on demand (it is
timing-dependent: the saga committed before the reply this time). The mechanism was exercised
directly instead: an `InventoryReservedEvent` for an order with no saga instance was published to the
broker, and Seq showed the new Warning at the default level.
