---
description: "Task list for Metrics - Prometheus and Grafana dashboards"
---

# Tasks: Metrics - Prometheus and Grafana dashboards

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [x] T001 Shared:
  - metrics in `AddObservability` (`METRICS_ENDPOINT`): ASP.NET Core, HttpClient, runtime, MassTransit, Npgsql, the
    shop's meters;
  - `AddSampledGauge(...)`: gauges sampled from the database on a schedule.
- [x] T002 Order: `AddOrderMetrics()` (orders by status, settle-time percentiles, its outbox)
- [x] T003 Every service with an outbox registers the backlog gauge
- [x] T004 Tests: the samplers read committed rows, never a rolled-back one; nothing registered without `METRICS_ENDPOINT`
- [x] T005 Compose: Prometheus (OTLP receiver, RabbitMQ scrape), Grafana (provisioned); the app overlay's `METRICS_ENDPOINT`; production unpublished, and the overlay check
- [x] T006 The dashboard JSON
- [x] T007 Live: a checkout run and a broker fault, read through Prometheus's API, and every panel's query run against it
- [x] T008 Docs: the observability guide, production, CLAUDE.md, `.env.example`, timeline, backlog
- [x] T009 Merged, closes #292 - #307

## Evidence

- `OrderMetricsTests` (Order.Tests), 5/5, against PostgreSQL:
  - a committed order is counted, and one in a rolled-back transaction is not;
  - settle percentiles over a window: 1 s, 2 s and 10 s give p50 2 and p95/p99 10, and an order paid outside the
    window is ignored;
  - an empty window reports nothing rather than zeros;
  - a sample that throws keeps the previous readings;
  - nothing is registered without `METRICS_ENDPOINT`.
- The Order suite: 371/371. The solution builds.
- Mutations, both caught:
  - a failed sample drops its readings;
  - the settle window is ignored.
- **Live, checkout** (`loadtest/run.sh checkout` on the rebuilt stack): `ecommerce_orders{status="Paid"}` went from
  9,744 to 9,942. That is +198, exactly the run's paid count.
- **Live, broker fault** (`loadtest/fault.sh broker`, 2026-10-05, RabbitMQ down 13:54:10 → 13:55:14 UTC). Order's
  outbox backlog from Prometheus:

  | Time | Order's outbox | |
  | :-- | --: | :-- |
  | 13:54:28 | 0 | |
  | 13:54:34 | 40 | broker down |
  | 13:55:16 | 310 | broker back |
  | 13:56:21 | 616 | still growing 67 s after recovery |
  | 13:56:49 | 0 | drained |

  Inventory's, the orchestrator's and Payment's outboxes rose and drained the same way. The late start is new evidence
  for #304, posted there.
- **Every dashboard panel's query** was run against Prometheus over that window and returned series: 16 panels, one query
  each, including the RabbitMQ per-queue depths and MassTransit's consume counts and failures.
  - The 5xx panel first counted `/health` answering 503 while the broker was down. Customers saw none, so the panel
    now leaves health probes out, like the others.
  - Grafana loads the provisioned "E-commerce overview" (20 panels including rows) and the Prometheus datasource.
  - A screenshot was not taken: signing in needs the Grafana admin password, which is the owner's to enter.
- **Found and filed: #306.** The same broker-fault run left one `ProcessPaymentCommand` in `ProcessPayment_error`
  (`23505` on `AK_InboxState_MessageId_ConsumerId`). It was a redelivery consumed twice at once; its twin paid the
  order, and every invariant held (361 placed, 361 paid, nothing held). The run's files are not in
  `loadtest/results/`, since the resilience report is about #291's runs.
- Production overlay: 25 services, only Caddy published, Prometheus and Grafana included and unpublished. The deploy
  tests: 19/19.
