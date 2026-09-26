# Implementation Plan: The Orchestrator answers /health

> Completed on 2026-09-27, after the feature merged (#155), from the code at that merge, the pull request and
> docs/architecture/microservices-design.md.

**Branch**: `071-orchestrator-health` | **Spec**: [spec.md](spec.md) | **Issue**: #115 | **PR**: #155 (merged 2026-09-26)

## Summary

Give the Orchestrator the `/health` every other service has - its saga database plus the broker - route it through
the gateway at `/api/orchestrator/health`, and make compose, CI and `verify-saga.sh` wait for it.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: ASP.NET Core health checks, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`
10.0.11 (new package on the Orchestrator), MassTransit's own bus health check, YARP;
`Microsoft.AspNetCore.Mvc.Testing` 10.0.12 in the test project

**Storage**: none new - the existing saga database on 5436

**Testing**: `WebApplicationFactory<Program>` with the MassTransit test harness, against a real PostgreSQL and an
unreachable one; live checks through the gateway

**Target Platform**: the Orchestrator (5058; 8080 in its container) behind the gateway

**Constraints**: same JSON as the other services' `/health`

**Scale/Scope**: one endpoint, one route, one cluster

## Design

- `Program.cs` gets `AddHealthChecks().AddDbContextCheck<OrchestratorDbContext>("orchestrator_postgres_db")` and
  `MapHealthChecks("/health")`, with the same JSON writer as Inventory's. MassTransit adds `masstransit-bus` to
  the health checks by itself, as Inventory's live response shows.
- **Gateway:**
  - an `orchestrator-cluster` pointing at `http://localhost:5058/`;
  - `orchestrator-health-route`, `/api/orchestrator/health` rewritten to `/health`;
  - in compose, `--ReverseProxy:Clusters:orchestrator-cluster:...=http://orchestrator:8080/`.
  - No other route goes to the Orchestrator: it serves nothing but its health.
- **Compose:** the Orchestrator uses the shared `service-healthcheck` anchor. The gateway `depends_on` it being
  healthy, like the others.
- **CI:** the saga job's readiness loop adds `orchestrator:5058`.
- **`verify-saga.sh`:** its stall message probes `/health` and reports the answer.

## Test

`Ecommerce.Orchestrator.Tests` gets a `HealthTests` using `WebApplicationFactory<Program>`: the endpoint answers
with both checks named. If that cannot host without RabbitMQ, the evidence is the CI job (which now waits for
it) plus the live check through the gateway, with the Orchestrator up and then stopped.

(It could host: `HealthTests` swaps the broker for `AddMassTransitTestHarness()`, removes the hosted services so the
payment-timeout sweeper does not read a saga table the test database lacks, and points the DbContext at a real
PostgreSQL on 5436 or at an unreachable port. Both tests pass; see [research.md](research.md) D3.)

## Constitution Check

Against all five principles of [constitution.md](../../.specify/memory/constitution.md):

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The Orchestrator reports on its own database and its own broker connection; nothing reads another service's state |
| **II. Clean Architecture Layering** | **Pass.** A transport concern in `Program.cs` of the WebApi project, where the Orchestrator's bus wiring already lives; no controller added |
| **III. Atomic Writes and Idempotent Messaging** | **Pass (not applicable).** Read-only probe; no write or message |
| **IV. Identity Comes From the Token** | **Pass.** `/health` is anonymous like every service's; it exposes check names and statuses only |
| **V. Evidence Over Assumption** | **Pass.** The feature exists because the principle's "a log line reporting a subsystem started is not evidence" applied to the Orchestrator, which had no probe at all. `HealthTests` run the real host against a real and an unreachable PostgreSQL, with a mutation check; the live run showed 200 / 502 / 200 through the gateway |

It also resolves the one recorded disagreement between CLAUDE.md and the constitution's service-topology rule that
every service exposes `/health`.

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/071-orchestrator-health/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── contracts/http-api.md
└── checklists/requirements.md
```

### Source Code (touched at the merge)

```text
server/src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/{Program.cs, Ecommerce.Orchestrator.WebApi.csproj}
server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json           # orchestrator-cluster, orchestrator-health-route
server/docker-compose.app.yml                                         # health check back on; gateway waits; cluster override
server/tests/Ecommerce.Orchestrator.Tests/{HealthTests.cs, Ecommerce.Orchestrator.Tests.csproj}
.github/workflows/ci.yml                                              # saga job waits for orchestrator:5058
.github/scripts/verify-saga.sh                                        # probes it; reports it on a stall
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

Nothing it set out to do. The Orchestrator still serves no other endpoint.
