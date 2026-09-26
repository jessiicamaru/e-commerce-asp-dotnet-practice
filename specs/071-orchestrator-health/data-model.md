# Data Model: The Orchestrator answers /health

> Written on 2026-09-27, after the feature merged (#155), from the code at that merge, the pull request and
> docs/architecture/microservices-design.md.

**Feature**: [spec.md](spec.md)

**No table, column, index or migration changed.** The feature adds a health endpoint over the Orchestrator's existing
saga database (`ecommerce_saga_db`, 5436), which the `orchestrator_postgres_db` check opens a connection to through
`OrchestratorDbContext`. It reads no row.

Configuration that changed:

| Where | What |
| :-- | :-- |
| `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json` | `ReverseProxy:Clusters:orchestrator-cluster` → `http://localhost:5058/`; `ReverseProxy:Routes:orchestrator-health-route` |
| `server/docker-compose.app.yml` | the Orchestrator on the `service-healthcheck` anchor (was `healthcheck: disable: true`); the gateway's `depends_on: orchestrator: condition: service_healthy`; the cluster address override `http://orchestrator:8080/` |
