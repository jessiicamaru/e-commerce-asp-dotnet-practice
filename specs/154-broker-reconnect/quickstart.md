# Quickstart: Services reconnect to the broker within seconds of its return

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter BrokerReconnectTests
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
./loadtest/fault.sh broker        # three times
docker logs ecommerce-order 2>&1 | grep "Retrying 00:00"
```

Expected:
- the tests pass;
- each run passes with every error queue empty, and the backlog clears 25 s or less after recovery;
- the log shows retries at most 5 s apart (`Retrying 00:00:05`), never `00:00:30`.
