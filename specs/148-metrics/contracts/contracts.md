# Contracts: Metrics - Prometheus and Grafana dashboards

No HTTP, message or gRPC change in the shop. New configuration:

| Setting | Where | Meaning |
| :-- | :-- | :-- |
| `METRICS_ENDPOINT` | every service | OTLP base for metrics, e.g. `http://prometheus:9090/api/v1/otlp`; `/v1/metrics` is appended. Unset: nothing exported. |
| `GRAFANA_ADMIN_PASSWORD` | compose | required, as `SEQ_ADMIN_PASSWORD` |

| Port (development) | What |
| :-- | :-- |
| 9090 | Prometheus (UI and API) |
| 3000 | Grafana: "E-commerce overview" |

In production neither is published: they are reached by an SSH tunnel, as Seq is.
