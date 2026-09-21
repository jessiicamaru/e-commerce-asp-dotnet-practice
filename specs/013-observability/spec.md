# Feature Specification: Following One Order Across Seven Services

**Feature Branch**: `013-observability` · **Created**: 2026-09-22 · **Status**: Implemented

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

### US1 — "Show me everything about order X" is one query (P1)

**Acceptance**: `OrderId = 'X'` in Seq returns the log lines of every service that logged about the
order; the trace behind them spans every service the checkout touched.

### US2 — The 2026-09-21 stall is visible at the default level (P1)

**Acceptance**: a reply that reaches the saga with no instance to correlate to is logged at
**Warning**, naming the order, the event and the likely cause — without restarting anything.

### US3 — Nothing secret is shipped (P1)

**Acceptance**: no token, password or parameter value appears in any shipped log line or span.

## Requirements

- **FR-001**: Seq in `docker-compose.yml`; its admin password comes from the environment and is required.
- **FR-002**: All seven services and the gateway ship structured logs and traces over OTLP when `OTLP_ENDPOINT` is set.
- **FR-003**: One trace per checkout, surviving HTTP, gRPC and the MassTransit hop.
- **FR-004**: Every log line written while consuming a message about an order carries `OrderId`.
- **FR-005**: Every saga transition is logged at Information with the order id; a missing instance at Warning.
- **FR-006**: The gateway ignores client-supplied trace context.
- **FR-007**: No request or response bodies, headers, or database parameter values are recorded.

## Success Criteria

- **SC-001**: One Seq query by `OrderId` returns lines from every service that logged about the order, all on one trace.
- **SC-002**: An orphaned saga reply produces a Warning at the default log level.
- **SC-003**: 0 events containing a JWT-looking string after an end-to-end run.
