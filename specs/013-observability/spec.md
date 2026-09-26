# Feature Specification: Following One Order Across Seven Services

> Completed on 2026-09-27, after the feature merged (#33), from the code at that merge, the pull
> request and docs/guides/observability.md (this feature has no page under docs/features/).

**Feature Branch**: `013-observability` · **Created**: 2026-09-22 · **Status**: Implemented

**Merged**: [#33](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/33), 2026-09-22 (02:06, UTC+7)

**Input**: Issue [#22](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/22) — "one order cannot be followed across six services"

## Why this exists

Each service logged to its own console. On 2026-09-21 a saga stalled for the eighteenth day running,
and the ordinary logs showed **nothing** — no exception, no fault. Diagnosing it took restarting the
orchestrator by hand with MassTransit at `Debug` and reading two durations and an absence.

## Decisions already taken

The owner asked for the recommended option instead of being asked (2026-09-22):

- **OpenTelemetry on the built-in `ILogger`, not Serilog.** Logs *and* traces through one standard;
  Seq ingests OTLP. The stall was a latency problem, which is what a trace shows at a glance.
- **Correlation is the W3C trace, minted at the gateway.** A client-sent `traceparent` is ignored at
  the edge, so no caller can choose or collide a trace id. Inside, HTTP, gRPC and MassTransit all
  propagate it — the broker hop included.
- **`OrderId` on every log line about an order**, added once by a MassTransit consume filter.
- **Telemetry is optional at runtime**: with no `OTLP_ENDPOINT`, nothing is exported and nothing
  fails. Losing telemetry must never stop checkout.
- **Retention and non-local destinations are out of scope** until deployment is.

## User Scenarios

The people served here are the operators and developers of the shop, not its customers: the question
each story answers is "what happened to this order?".

### US1 — "Show me everything about order X" is one query (P1)

**Acceptance**: `OrderId = 'X'` in Seq returns the log lines of every service that logged about the
order; the trace behind them spans every service the checkout touched.

**Why this priority**: it is issue #22 in one sentence. Without it every other story is still seven
consoles read side by side.

**Independent Test**: place one order with the containers running and Seq up, then query
`OrderId = '<id>'` and open the trace of any line returned.

**Acceptance Scenarios**:

1. **Given** a checkout that reached `Paid`, **When** Seq is queried by its `OrderId`, **Then** lines
   from Order, the Orchestrator, Inventory and Payment are returned, all carrying one trace id.
2. **Given** one of those lines, **When** its trace is opened, **Then** it spans the gateway and every
   service the checkout called - over HTTP, over gRPC and across the broker.
3. **Given** a consumer that logs deep inside a handler that knows nothing about scopes, **When** it
   handles a message about an order, **Then** its line still carries `OrderId`.

### US2 — The 2026-09-21 stall is visible at the default level (P1)

**Acceptance**: a reply that reaches the saga with no instance to correlate to is logged at
**Warning**, naming the order, the event and the likely cause — without restarting anything.

**Why this priority**: equal first. The stall was invisible for eighteen days because the discard made
no sound; a trace does not help if nobody knows to look.

**Independent Test**: publish an `InventoryReservedEvent` for an order id with no saga instance and
look for the Warning in Seq at the default log level.

**Acceptance Scenarios**:

1. **Given** no saga instance for an order, **When** `InventoryReservedEvent`,
   `InventoryReservationFailedEvent`, `PaymentProcessedEvent` or `PaymentFailedEvent` arrives for it,
   **Then** a Warning names the order and the event and says the reply may have overtaken the
   submission's commit.
2. **Given** a normal checkout, **When** the saga moves, **Then** every transition is logged at
   Information with the order id.

### US3 — Nothing secret is shipped (P1)

**Acceptance**: no token, password or parameter value appears in any shipped log line or span.

**Why this priority**: equal first, because a telemetry store that holds bearer tokens turns a
debugging aid into a way to impersonate every customer.

**Independent Test**: after an end-to-end run with telemetry on, search Seq for anything shaped like a
JWT (`eyJ...`).

**Acceptance Scenarios**:

1. **Given** a run of `verify-saga.sh` and `verify-auth.sh` with telemetry on, **When** Seq is searched
   for JWT-looking strings, **Then** there are none.
2. **Given** a database command, **When** it is traced, **Then** the span carries the statement and its
   duration, never the parameter values.

### Edge Cases

- **No collector.** `OTLP_ENDPOINT` unset (CI, a bare `dotnet run`): nothing is exported and the
  service behaves exactly as before. This is a deliberate exception to failing fast on missing
  configuration.
- **A client sends its own `traceparent`.** The gateway ignores it and starts a new trace.
- **A message that is not about an order** (product and stock events): the filter passes it through
  untouched; it carries no `OrderId`.
- **Health probes.** Docker calls `/health` every few seconds; those requests are not traced, or they
  would bury the checkouts.
- **The race cannot be reproduced on demand.** It is timing-dependent, so the Warning is proved by
  exercising the mechanism directly (research D7), not by re-creating the stall.

## Requirements

- **FR-001**: Seq in `docker-compose.yml`; its admin password comes from the environment and is required.
- **FR-002**: All seven services and the gateway ship structured logs and traces over OTLP when `OTLP_ENDPOINT` is set.
- **FR-003**: One trace per checkout, surviving HTTP, gRPC and the MassTransit hop.
- **FR-004**: Every log line written while consuming a message about an order carries `OrderId`.
- **FR-005**: Every saga transition is logged at Information with the order id; a missing instance at Warning.
- **FR-006**: The gateway ignores client-supplied trace context.
- **FR-007**: No request or response bodies, headers, or database parameter values are recorded.
- **FR-008**: With `OTLP_ENDPOINT` unset, a service MUST start and serve normally and export nothing.
- **FR-009**: Checkout MUST log the id of the order it creates and tag its span with it, so the trace
  carries the order id from the first service onward.
- **FR-010**: Per-statement database logging MUST NOT flood an order's log lines; the statement is
  recorded once, on its span.

### Key Entities

- **Trace** - one checkout's path through the system, started at the gateway, identified by a W3C
  trace id and carried across HTTP, gRPC and message headers.
- **Log event** - one structured line with its properties (among them `OrderId` when the work concerned
  an order), its level and the trace it belongs to.
- **Saga transition** - one move of the checkout state machine, now written as an Information event.

## Success Criteria

- **SC-001**: One Seq query by `OrderId` returns lines from every service that logged about the order, all on one trace.
- **SC-002**: An orphaned saga reply produces a Warning at the default log level.
- **SC-003**: 0 events containing a JWT-looking string after an end-to-end run.
- **SC-004**: With no `OTLP_ENDPOINT`, the test suite and both CI smoke scripts pass unchanged.

## Assumptions

- Seq runs locally beside the other infrastructure; it is a development and diagnosis tool, not a
  production telemetry pipeline.
- The instrumentation libraries do not capture headers, bodies or SQL parameters by default, and
  nothing here turns that on (research D5).
- Every contract that concerns an order names its id `OrderId`, as a `Guid`.

## Out of Scope

- Retention, sampling, alerting and any destination other than a local Seq.
- Metrics (counters, histograms); this feature ships logs and traces only.
- Reproducing the 2026-09-21 race on demand.
