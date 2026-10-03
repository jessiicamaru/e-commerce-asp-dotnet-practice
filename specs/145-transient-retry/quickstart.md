# Quickstart: A consumer survives a transient database failure

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter TransientRetry
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
./loadtest/run.sh checkout          # from the #290 branch
docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages | grep _error
```

Expected:
- the tests pass;
- the checkout run reports units sold = units deducted, nothing held, and exits 0;
- every `_error` queue reads 0.
