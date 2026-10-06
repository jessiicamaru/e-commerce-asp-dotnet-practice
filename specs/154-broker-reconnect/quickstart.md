# Quickstart: Services reconnect to the broker within seconds of its return

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter BrokerReconnectTests
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
./loadtest/fault.sh broker        # three times: prints when the broker opened its port and each service's lag
# the baseline, on the same stack: MassTransit's own schedule
#   Messaging__ReconnectQuickly=false on the eight services (an override file), then fault.sh broker three times
docker logs ecommerce-order 2>&1 | grep "Retrying 00:00"
```

Expected:
- the tests pass;
- each run passes with every error queue empty, and every service reconnects within 6 s of the broker's port opening;
- with the switch off, the slowest service takes clearly longer;
- the log shows retries at most 5 s apart (`Retrying 00:00:05`), never `00:00:30`.
