# Quickstart: Metrics - Prometheus and Grafana dashboards

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
open http://localhost:3000            # Grafana, "E-commerce overview" (admin / GRAFANA_ADMIN_PASSWORD)
./loadtest/run.sh checkout            # watch it fill
curl -s 'http://localhost:9090/api/v1/query?query=ecommerce_orders{status="Paid"}'
./loadtest/fault.sh broker            # watch Order's outbox backlog rise and drain
```

Expected:
- every panel has data;
- `ecommerce_orders{status="Paid"}` rises by the run's paid count;
- during the broker outage, `ecommerce_outbox_pending_messages{job="ecommerce-order"}` rises, then falls to zero.
