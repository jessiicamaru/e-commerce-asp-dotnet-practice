# Quickstart: MassTransit back on 8.3.6 after a broker outage stopped consumers for good

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter "MassTransitVersionTests|BrokerReconnectTests"
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
./loadtest/fault.sh broker        # three times or more
docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages consumers | awk '$2 > 0 && $3 == 0'
```

Expected:
- the tests pass; changing one reference to 8.5.11 makes `Every_project_references_MassTransit_8_3` fail, naming it;
- every run prints PASSED with no error queue holding messages;
- the last command prints nothing after each run: no queue with messages and no consumer.
