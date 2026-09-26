# Implementation Plan: The Orchestrator answers /health

**Branch**: `071-orchestrator-health` | **Spec**: [spec.md](spec.md) | **Issue**: #115

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
