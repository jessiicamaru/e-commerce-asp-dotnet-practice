# HTTP Contract: The Orchestrator answers /health

> Written on 2026-09-27, after the feature merged (#155), from the code at that merge, the pull request and
> docs/architecture/microservices-design.md.

**Feature**: [spec.md](../spec.md)

No message contract changed. One endpoint on the Orchestrator and one gateway route.

## `GET /health` - Orchestrator, anonymous

Direct: `http://localhost:5058/health` (`http://orchestrator:8080/health` inside compose).
Through the gateway: **`GET http://localhost:5000/api/orchestrator/health`**, rewritten to `/health` by
`orchestrator-health-route` on the new `orchestrator-cluster`.

```json
{
  "status": "Healthy",
  "service": "Orchestrator",
  "checks": [
    { "name": "masstransit-bus",          "status": "Healthy", "description": "…", "duration": "…" },
    { "name": "orchestrator_postgres_db", "status": "Healthy", "description": null, "duration": "…" }
  ]
}
```

| Status | When |
| :-- | :-- |
| 200 | Both checks healthy |
| 503 | A check unhealthy - tested with the saga database unreachable |
| 502 (gateway) | The Orchestrator is not running - observed in the PR's live run |

The shape matches the other services' `/health`. No other gateway route reaches the Orchestrator.
