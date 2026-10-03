# Feature Specification: Metrics - Prometheus and Grafana dashboards

**Feature Branch**: `feat/292-metrics`
**Created**: 2026-10-04
**Status**: Draft
**Issue**: #292
**Input**: "Seq holds logs and traces (specs/013), but there are no metrics: request rates, latencies, saga outcomes,
outbox depth or queue lengths over time."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See how the shop is running, now and over the last hour (Priority: P1)

An operator opens one dashboard and sees, for every service:
- requests a second, latency (p50/p95/p99) and errors;
- checkouts by outcome, and how long orders take to settle;
- what is waiting in each outbox;
- the broker's queues.

All of it is live while customers buy, and kept for later.

**Why this priority**: the load and resilience runs (specs/144, 147) produced numbers only after each run. An operator
needs them while it happens. A thesis needs graphs of the system under load and under fault.

**Independent Test**:
1. Start the compose stack and run `server/loadtest/run.sh checkout`.
2. Open Grafana's "E-commerce overview": every panel has data.
3. Its order counts agree with the run's readings.

**Acceptance Scenarios**:

1. **Given** the compose stack, **When** customers check out, **Then** the dashboard shows:
   - requests and latency per service;
   - paid orders rising by exactly as many as were paid;
   - the settle-time percentiles.
2. **Given** RabbitMQ stopped during checkouts (`fault.sh broker`), **When** the dashboard is watched, **Then** Order's
   outbox backlog rises during the outage and drains after it.
3. **Given** a service started with no metrics endpoint configured, **When** it runs, **Then** it exports nothing and
   behaves as before, as with traces (specs/013).

### Edge Cases

- **A retried consume** (#299): order counts come from committed rows, so a rolled-back attempt counts nothing.
- **A redelivered settlement**: the same; the row's status is counted, not the message.
- **Prometheus or Grafana down**: the services carry on. Exporting is best effort.
- **Production**: Prometheus and Grafana publish no port, like Seq (specs/141). The production overlay's check
  asserts it.

## Requirements *(mandatory)*

- **FR-001**: Every service exports OpenTelemetry metrics when `METRICS_ENDPOINT` is set:
  - ASP.NET Core, HttpClient, runtime;
  - MassTransit (consumes, faults, durations);
  - Npgsql.
- **FR-002**: Domain gauges, read from committed rows on a schedule, never counted in a transaction:
  - Order: orders by status; settle-time percentiles over the last minute;
  - every service with an outbox: messages pending in it.
- **FR-003**: Prometheus (OTLP receiver on) and Grafana in compose. Grafana's datasource and dashboard are provisioned
  from the repository. RabbitMQ's own Prometheus plugin is scraped.
- **FR-004**: The dashboard is versioned in the repository, and shows traffic, latency and errors per service,
  checkout outcomes, settle time, outbox backlog and the broker.
- **FR-005**: Grafana's admin password is required by compose, as Seq's is.

## Success Criteria *(mandatory)*

- **SC-001**: During a checkout run every dashboard panel has data, and the paid-orders gauge rises by the number the
  run paid.
- **SC-002**: During a broker outage Order's outbox backlog is visible rising and draining.
- **SC-003**: With `METRICS_ENDPOINT` unset, nothing is exported and every existing test passes.

## Assumptions

- Prometheus 3.5 accepts OTLP metrics with `--web.enable-otlp-receiver`, and labels each series with `job` from
  `service.name`.
- RabbitMQ's `rabbitmq_prometheus` plugin is already enabled in the management image (port 15692).
