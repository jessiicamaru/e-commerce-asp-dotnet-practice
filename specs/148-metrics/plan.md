# Implementation Plan: Metrics - Prometheus and Grafana dashboards

**Branch**: `feat/292-metrics` | **Date**: 2026-10-04 | **Spec**: [spec.md](spec.md) | **Issue**: #292

## Summary

- **Shared:** `AddObservability` gains metrics. When `METRICS_ENDPOINT` is set, a meter provider exports over OTLP:
  - ASP.NET Core, HttpClient and runtime instrumentation;
  - the `MassTransit` and `Npgsql` meters;
  - the shop's own meters.
- **Shared:** `AddSampledGauge(name, unit, description, sample)` - one sampler for every gauge read from the database,
  on a schedule. Each service with an outbox registers its `OutboxMessage` count with it. (Planned as a dedicated
  `AddOutboxBacklogGauge<TDbContext>()`; built as the general method once Order's gauges needed the same machinery.)
- **Order:** samples its orders by status and its settle-time percentiles.
- **Compose:** Prometheus (OTLP receiver, scraping RabbitMQ) and Grafana (provisioned datasource and dashboard) join
  the infrastructure file. The app overlay points every service at Prometheus. The production overlay keeps both
  unpublished.

## Technical Context

**Language/Version**: C# / .NET 10; Prometheus 3.5; Grafana 12.1
**Primary Dependencies**:
- `OpenTelemetry.Extensions.Hosting`;
- the OTLP exporter (already referenced);
- `OpenTelemetry.Instrumentation.Runtime` 1.19.0 (new);
- MassTransit's and Npgsql's built-in meters.
**Storage**: Prometheus's own volume; no table changes
**Testing**:
- a test that the sampler reads committed rows and ignores a rolled-back one;
- the existing suites with `METRICS_ENDPOINT` unset;
- the dashboard live during a checkout run and a broker fault, read through Prometheus's API.
**Target Platform**: the compose stack; production with ports unpublished
**Performance Goals**: sampling every 15 s, one indexed count per service
**Constraints**: metrics never fail a request, never live in a transaction, never export when unconfigured
**Scale/Scope**: Shared extension, two samplers, compose, provisioning, one dashboard, docs

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service reports its own numbers from its own database; nothing reads another service's tables. |
| **II. Clean Architecture Layering** | **Pass.** Samplers live in Infrastructure, which reads the database. The exporter is wired in WebApi through Shared. Application code is untouched. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, and why the design is gauges.** A counter bumped inside a consume can be rolled back and retried (#299), counting twice. Gauges read committed rows only. |
| **IV. Identity Comes From the Token** | **Not applicable.** Metrics carry no user, order or token. Labels are services, statuses, routes and message types only. |
| **V. Evidence Over Assumption** | **Planned.** A sampler test against PostgreSQL; the paid gauge set against the load run's own count; the outbox gauge watched through a broker outage. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/148-metrics/                                         the record
server/src/BuildingBlocks/Ecommerce.Shared/Observability/   metrics in AddObservability; SampledGauges (AddSampledGauge)
server/src/Services/*/WebApi/Program.cs                      each outbox's backlog gauge
server/src/Services/Order/Ecommerce.Order.Infrastructure/   Persistence/OrderMetrics (AddOrderMetrics)
server/observability/prometheus.yml                         RabbitMQ scrape; OTLP receiver flags in compose
server/observability/grafana/provisioning/                  datasource, dashboard provider
server/observability/grafana/dashboards/ecommerce.json      the dashboard
server/observability/grafana/generate_dashboard.py          writes it
server/docker-compose.yml, docker-compose.app.yml, docker-compose.prod.yml
```

## Complexity Tracking

No violation to justify.
