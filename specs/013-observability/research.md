# Research: Following One Order Across Seven Services

> Completed on 2026-09-27, after the feature merged (#33), from the code at that merge, the pull
> request and docs/guides/observability.md (this feature has no page under docs/features/).

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-22

Seven decisions. D1 and D3 were taken on the owner's instruction to choose the recommended option; the
rest are how the build carried them out. Where a decision was first recorded without its rationale or
rejected alternatives written out, those are added below the original text.

## D1 — OpenTelemetry, not Serilog

**Decision**: `Microsoft.Extensions.Logging` + OpenTelemetry (logs and traces), OTLP over HTTP/protobuf
to Seq's `/ingest/otlp/v1/{logs,traces}`. Packages 1.19 in `Ecommerce.Shared`, wired by one call,
`builder.AddObservability("<service>")`.

**Rejected**: Serilog + Seq sink (the roadmap's original plan) — logs only, one vendor's pipeline, and
the stall was a *latency* finding that a trace shows directly. Seq ingests OTLP, so nothing is lost.

**Rationale, as built**: every service already logged through `ILogger`, so no call site changes; the
exporter is added to the logging pipeline with `IncludeScopes = true` (the `OrderId` scope is what US1
queries) and `ParseStateValues = true` (message-template properties become queryable properties in
Seq). Each service is named `ecommerce-<service>` in the resource. `OTLP_API_KEY`, when set, is sent as
Seq's `X-Seq-ApiKey` header; nothing sets it locally.

## D2 — What produces spans

ASP.NET Core (server, including gRPC), HttpClient (so every gRPC client call), MassTransit
(`ActivitySource "MassTransit"`: send, publish, consume, saga) and Npgsql (one span per command,
statement text only). Health probes are filtered out — Docker calls them every few seconds.

**Rationale**: these four are exactly the hops a checkout crosses - the gateway and the services' HTTP
endpoints, Order's gRPC calls to Cart, Identity and Catalog, the broker, and the databases. A gRPC
client call is an HttpClient request underneath, so the HTTP instrumentation covers it without a gRPC
package. The health filter is `!ctx.Request.Path.StartsWithSegments("/health")`.

**Alternatives considered**: EF Core instrumentation instead of Npgsql's source. Not chosen: the Npgsql
span already carries each statement and its duration, which is what D5 relies on to lower EF's own
per-command logging. Whether an EF Core instrumentation package was evaluated is not recorded.

## D3 — Where the trace starts

**Decision**: the gateway registers a `DistributedContextPropagator` that **ignores** incoming trace
context and injects normally. ASP.NET Core's hosting layer takes the propagator from DI, so the
gateway's request activity is always a new root.

**Rejected**: accepting `traceparent` from callers — convenient, and it lets any caller pick a trace id,
collide two checkouts into one trace, or attach to someone else's.

**Rationale, as built**: `IgnoreIncomingTraceContextPropagator` returns no trace id, no state and no
baggage from `Extract*`, and delegates `Inject` and `Fields` to the default propagator, so YARP and
HttpClient still pass the gateway's own context on to the services. Only the gateway registers it;
inside the system every service propagates normally.

## D4 — `OrderId` without touching every handler

A MassTransit consume filter (`OrderIdLogScopeFilter<T>`) reads a `Guid OrderId` property off the
message (reflected once per type), opens a logging scope with it and tags the span `order.id`.
Registered once per bus with `cfg.UseConsumeFilter(typeof(OrderIdLogScopeFilter<>), context)`. Checkout
itself logs the order id when it creates it.

**Rationale**: every contract that concerns an order names its id `OrderId` as a `Guid`, so one
property lookup covers them all; a message without one (product and stock events) passes through
untouched. The lookup is cached in a `ConcurrentDictionary` per message type, so reflection runs once.
The saga is a consumer too, so its log lines get the scope from the same filter.

**Alternatives considered**: a `BeginScope` in each consumer and in the saga. Rejected: dozens of call
sites, and the one somebody forgets is the line that goes missing from the query - the silent failure
this feature exists to end.

## D5 — Secrets

Nothing used here records headers or bodies by default, and nothing turns that on. EF Core logs
parameters as `'?'` unless sensitive-data logging is enabled; it is not. EF's per-command Information
logging is lowered to Warning in every service — it made up most of an order's log lines and the
Npgsql span already carries each statement and its duration.

**Decision**: rely on the instrumentation's defaults and change none of them; set
`Microsoft.EntityFrameworkCore.Database.Command` to `Warning` in every service's `appsettings.json`
(the Orchestrator gained an `appsettings.json` for it).

**Rationale**: the effect was measured - one order's `OrderId` query returned 151 lines before the
change and 8 after. The guarantee that no token is recorded is checked, not assumed: 0 JWT-shaped
strings in Seq after an end-to-end run (SC-003).

**Alternatives considered**: enabling request/response enrichment for easier debugging. Rejected: a
header is where the bearer token travels. Any change here needs a reason written in this decision
(`docs/guides/observability.md` says the same).

## D6 — Seq's admin password

Seq 2024 refuses to start with an empty first-run password, and forces a change at the first login.
`SEQ_ADMIN_PASSWORD` is therefore required by `docker-compose.yml` (`${VAR:?}`); `.env.example`
documents that the value is only the initial one.

**Decision**: `SEQ_FIRSTRUN_ADMINPASSWORD: ${SEQ_ADMIN_PASSWORD:?...}` - compose refuses to start
without it.

**Alternatives considered**: running Seq without authentication. Rejected: the compose file's comment
says so directly - "running it without authentication is not an option this file offers" - because the
store holds every service's logs.

## D7 — Proving the stall is now visible

Reverting the orchestrator's outbox on a scratch build did **not** reproduce the race on demand (it is
timing-dependent: the saga committed before the reply this time). The mechanism was exercised
directly instead: an `InventoryReservedEvent` for an order with no saga instance was published to the
broker, and Seq showed the new Warning at the default level.

**Decision**: prove the mechanism (a reply that finds no instance), not the race.

**Rationale**: constitution principle V asks for the real path to be exercised and for what could not
be verified to be named. The Warning comes from `OnMissingInstance` on each of the four reply events;
publishing one such reply exercises exactly that code at the default level. The race's cause was fixed
separately (#15, the orchestrator's outbox).

**Alternatives considered**: an automated test of the Warning. Not written; whether it was considered
is not recorded. The saga had no tests at all until specs/053.
