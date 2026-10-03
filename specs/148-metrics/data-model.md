# Data Model: Metrics - Prometheus and Grafana dashboards

No table, column or migration. The samplers read existing tables.

## The shop's own metrics

| Metric (as Prometheus names it) | Type | Labels | From |
| :-- | :-- | :-- | :-- |
| `ecommerce_orders` | gauge | `status` | Order: `SELECT "Status", count(*) FROM orders GROUP BY "Status"`, every 15 s |
| `ecommerce_order_settle_seconds` | gauge | `quantile` (0.5, 0.95, 0.99) | Order: percentiles of `"PaidAt" - "CreatedAt"` over orders paid in the last 60 s |
| `ecommerce_outbox_pending_messages` | gauge | `job` (the service) | every service with an outbox: `count(*)` of `"OutboxMessage"` |

## The libraries' metrics (names as exported)

- **ASP.NET Core:** `http_server_request_duration_seconds` (histogram: method, route, status code).
- **HttpClient:** `http_client_request_duration_seconds`.
- **Runtime:** `process_runtime_dotnet_*` (GC, thread pool, memory).
- **MassTransit:** `messaging_masstransit_*`, the consume counts, faults and durations by message type.
- **Npgsql:** `db_client_*` (connections, command durations).
- **RabbitMQ:** `rabbitmq_*` (queue messages, consumers, publishes).

Every series carries `job` = the service name (`ecommerce-order`, ...), from OTLP's `service.name`.
