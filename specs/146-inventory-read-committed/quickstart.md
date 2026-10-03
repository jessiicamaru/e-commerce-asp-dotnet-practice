# Quickstart: Checkouts of one product do not queue behind retries

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
- the `40001` count is near zero, against thousands before;
- the checkout settle-time median is near 1 s.
