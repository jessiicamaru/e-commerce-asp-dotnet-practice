# Implementation Plan: Following One Order Across Seven Services

> Written on 2026-09-27, after the feature merged (#33), from the code at that merge, the pull request
> and docs/guides/observability.md (this feature has no page under docs/features/).

**Branch**: `013-observability` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013-observability/spec.md`

## Summary

Give every service and the gateway one call, `builder.AddObservability("<service>")`, in
`Ecommerce.Shared/Observability/`, which ships structured logs and distributed traces over OTLP to Seq
when `OTLP_ENDPOINT` is set and does nothing when it is not. W3C trace context already flows through
ASP.NET Core, HttpClient (and so gRPC) and MassTransit's message headers, so one checkout becomes one
trace once each hop is instrumented; the gateway is made the only place a trace can begin. A MassTransit
consume filter adds `OrderId` to every log line written while a message about an order is handled, and
the saga logs its transitions at Information and a reply with no instance at Warning. Seq joins
`docker-compose.yml`. Decisions and rejected alternatives: [research.md](./research.md).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: OpenTelemetry for .NET 1.19 in `Ecommerce.Shared`
(`OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.19.1, `OpenTelemetry.Extensions.Hosting` 1.19.1,
`OpenTelemetry.Instrumentation.AspNetCore` 1.19.0, `OpenTelemetry.Instrumentation.Http` 1.19.0);
MassTransit 8.3.6's own `ActivitySource "MassTransit"`; Npgsql's `ActivitySource "Npgsql"`. Seq
`datalust/seq:2024.3` in compose.

**Storage**: No database change. Seq keeps what it ingests in the `seq_data` volume.

**Testing**: No automated test was added (see Complexity Tracking). Verified by hand against the
containers with Seq running: an `OrderId` query, the trace behind it, an orphaned reply published to
the broker, and a search for JWT-shaped strings. `dotnet test`, `verify-saga.sh` and `verify-auth.sh`
re-run with telemetry on.

**Target Platform**: All seven services and the gateway, in containers (`OTLP_ENDPOINT` set by
`docker-compose.app.yml` to `http://seq:5341/ingest/otlp`) or on the host (`.env`).

**Project Type**: Cross-cutting infrastructure in `Ecommerce.Shared`, wired into each service's
`Program.cs`.

**Performance Goals**: None stated. EF's per-statement Information logging was lowered to Warning
because it made up most of an order's lines (151 before, 8 after, for one order).

**Constraints**: Telemetry must never stop checkout (optional at runtime). No header, body or SQL
parameter may be recorded. A caller must not be able to choose a trace id.

**Scale/Scope**: 7 services + gateway; the consume filter on the six buses that consume order messages
(Cart, Catalog, Inventory, Orchestrator, Order, Payment - Identity had no MassTransit bus at the time).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. This plan was not
written before the build; the assessment below is made against the code at the merge.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** No service reads another's data. The shared code added lives in `Ecommerce.Shared`, which the principle names as the place for cross-cutting infrastructure; `Ecommerce.Contracts` is untouched |
| **II. Clean Architecture Layering** | **Pass.** Telemetry is wired in WebApi (`Program.cs`) and the gateway. The one Application-layer change - `SubmitOrderCommandHandler` logging the new order id and tagging `Activity.Current` - uses `Microsoft.Extensions.Logging` and `System.Diagnostics`, which are abstractions, not a transport or exporter |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** No write path changed. The checkout log line is written after the single `SaveChangesAsync`, so it describes an order that exists. The saga's Warning makes no state change: the orphaned reply is still discarded, as before, rather than redelivered for ever |
| **IV. Identity Comes From the Token** | **Pass.** No endpoint or identity path changed. The principle's concern here is the opposite direction - that a token is never *recorded* - which FR-007 and SC-003 cover: 0 JWT-shaped strings in Seq after an end-to-end run |
| **V. Evidence Over Assumption** | **Pass, with one stated gap.** Each claim was checked against the running containers and the output recorded (8 lines, one trace, the Warning text, 0 JWTs). The stall itself could not be reproduced on demand; the record says so plainly and proves the mechanism instead (research D7). No automated test guards the Warning or the filter |

**Post-design re-check**: no violations. The one deliberate departure from a constitution rule is
recorded rather than hidden: the configuration section asks a service to fail at startup on missing
configuration, and `OTLP_ENDPOINT` unset is accepted silently. The reason - losing telemetry must never
stop checkout - is in the spec's decisions and in `ObservabilityExtensions`' own comment.

## Project Structure

### Documentation (this feature)

```text
specs/013-observability/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Seven decisions: OpenTelemetry, spans, trace root, OrderId, secrets, Seq password, proof
├── data-model.md        # No table changed; what Seq keeps and what the log events carry
├── quickstart.md        # The Seq queries and checks, with expected results
├── contracts/
│   ├── http-api.md      # The gateway ignores traceparent; OTLP export; nothing added to any API
│   └── messages.md      # No record changed; trace context rides in MassTransit headers
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task list, all done
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/Ecommerce.Shared/
├── Ecommerce.Shared.csproj                  # the four OpenTelemetry packages
└── Observability/
    ├── ObservabilityExtensions.cs           # AddObservability(serviceName)
    ├── OrderIdLogScopeFilter.cs             # OrderId scope + order.id tag per consumed message
    └── IgnoreIncomingTraceContextPropagator.cs

server/src/ApiGateway/Ecommerce.ApiGateway/  # Program.cs registers the propagator; csproj
server/src/Services/*/Ecommerce.*.WebApi/    # Program.cs: AddObservability + the consume filter;
                                             # appsettings.json: EF command logging to Warning
server/src/Services/Orchestrator/.../StateMachines/OrderStateMachine.cs   # Transition, MissingInstance
server/src/Services/Orchestrator/.../appsettings.json                     # new file
server/src/Services/Order/.../SubmitOrder/SubmitOrderCommandHandler.cs    # logs and tags the order id
server/docker-compose.yml                    # seq, seq_data
server/docker-compose.app.yml                # OTLP_ENDPOINT for every container
server/.env.example                          # SEQ_ADMIN_PASSWORD, OTLP_ENDPOINT
docs/guides/observability.md, docs/architecture/saga-orchestration-roadmap.md (Phase 7), docs/README.md, CLAUDE.md
```

**Structure Decision**: one extension method in `Ecommerce.Shared`, called by every service, rather
than configuration repeated in eight `Program.cs` files - the same shape as `AddJwtAuthentication`. The
filter is generic (`OrderIdLogScopeFilter<T>`) and registered once per bus with
`cfg.UseConsumeFilter(typeof(OrderIdLogScopeFilter<>), context)`, so no consumer changes.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

The absence of automated tests is not a violation - principle V asks for the real path to be exercised
and shown, which the pull request did - but it is a gap worth naming: nothing fails in CI if the
consume filter is unregistered or the Warning is removed.

## What this feature does not finish

- **The Orchestrator still has no `/health`** and so no health route; that stayed open until specs/071.
- **CI runs no Seq.** With no `OTLP_ENDPOINT` nothing is exported there, so no job changed.
- **Retention, sampling, alerting and non-local destinations** wait for a deployment to exist.
- **Seq forces a password change at the first login**; `SEQ_ADMIN_PASSWORD` is only the initial one.
