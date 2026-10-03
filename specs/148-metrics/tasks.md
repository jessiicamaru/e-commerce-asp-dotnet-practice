---
description: "Task list for Metrics - Prometheus and Grafana dashboards"
---

# Tasks: Metrics - Prometheus and Grafana dashboards

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [ ] T001 Shared:
  - metrics in `AddObservability` (`METRICS_ENDPOINT`): ASP.NET Core, HttpClient, runtime, MassTransit, Npgsql, the
    shop's meters;
  - `AddOutboxBacklogGauge<TDbContext>()`.
- [ ] T002 Order: `OrderMetricsSampler` (orders by status, settle-time percentiles)
- [ ] T003 Every service with an outbox registers the backlog gauge
- [ ] T004 Tests: the samplers read committed rows, never a rolled-back one; nothing registered without `METRICS_ENDPOINT`
- [ ] T005 Compose: Prometheus (OTLP receiver, RabbitMQ scrape), Grafana (provisioned); the app overlay's `METRICS_ENDPOINT`; production unpublished, and the overlay check
- [ ] T006 The dashboard JSON
- [ ] T007 Live: a checkout run and a broker fault, read through Prometheus's API, and a screenshot of the dashboard
- [ ] T008 Docs: the observability guide, production, CLAUDE.md, `.env.example`, timeline, backlog
- [ ] T009 Merged, closes #292

## Evidence

(Filled in when the work is verified.)
