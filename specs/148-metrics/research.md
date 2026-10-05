# Research: Metrics - Prometheus and Grafana dashboards

## D1. Push over OTLP to Prometheus, not a /metrics endpoint per service

**Decision**: every service pushes OTLP metrics to Prometheus's own OTLP receiver (`--web.enable-otlp-receiver`,
`/api/v1/otlp/v1/metrics`).

**Rationale**:
- The services already export logs and traces over OTLP with the stable exporter package.
- A scrape endpoint would be a new public path on every service, which the gateway would route unless excluded.
- The Prometheus exporter for ASP.NET Core is still a beta package.

**Alternatives rejected**:
- An OpenTelemetry Collector in between: one more container to configure, for no feature these dashboards need.
- `/metrics` scraped per service: as above.

## D2. Gauges from committed rows, not counters in handlers

**Decision**: orders by status, settle-time percentiles and outbox backlog are sampled from the database every 15 s
and reported as observable gauges.

**Rationale**:
- Order settles inside the consumer's transaction, which #299's retry may roll back and run again. A counter
  incremented there counts an attempt, not an order.
- The database is the truth: the gauge says what is committed.

**Alternative rejected**: a counter after commit. MassTransit's outbox commits after the handler returns, so a
handler has no "after commit" hook to use.

## D3. Settle time as a gauge of recent percentiles

**Decision**: Order samples p50/p95/p99 of `PaidAt - CreatedAt` over orders paid in the last 60 s, as
`ecommerce_order_settle_seconds{quantile}`.

**Rationale**:
- A histogram would need recording at settlement: the same transaction problem as D2.
- Percentiles over a sliding minute are what the load reports already show, now continuous.

## D4. RabbitMQ through its own plugin

**Decision**: Prometheus scrapes `rabbitmq:15692/metrics`, from the plugin the management image already enables.

**Rationale**: queue depths, consumers and publish rates, exactly as the broker counts them.

## D5. Grafana provisioned from files

**Decision**: the datasource and one dashboard JSON live in `server/observability/grafana/` and are mounted
read-only. The admin password is required (`GRAFANA_ADMIN_PASSWORD`, as Seq's).

**Rationale**: a dashboard in the repository is reviewed and versioned like code (the issue's acceptance), and a fresh
stack shows it with no clicking.

## D6. Off unless configured

**Decision**: no `METRICS_ENDPOINT`, no meter provider. This is the same rule as `OTLP_ENDPOINT` (specs/013).

**Rationale**: CI, tests and a bare `dotnet run` need no Prometheus. Losing metrics must never stop checkout.
