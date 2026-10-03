# Quickstart: Inventory consumes without serialization aborts

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
./loadtest/run.sh checkout
./loadtest/run.sh race     # three times
docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages | grep _error
docker logs --since 10m ecommerce-inventory 2>&1 | grep -c 40001
```

Expected:
- every run exits 0, and every `_error` queue reads 0;
- the `40001` count is zero, against thousands at `REPEATABLE READ`;
- latency is judged over several warm runs per isolation level (one warm-up run each, discarded), from Order's own
  `CreatedAt` -> `PaidAt`, because single runs on a laptop swing between a 0.3 s and a 10 s median.
