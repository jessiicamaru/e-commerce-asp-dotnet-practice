# Quickstart: A message delivered twice at once is consumed once and faults neither time

```bash
cd server
DB_PASSWORD=... dotnet test tests/Ecommerce.Payment.Tests --filter InboxRedeliveryTests
DB_PASSWORD=... dotnet test tests/Ecommerce.Inventory.Tests --filter TransientRetryTests
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
./loadtest/fault.sh broker
```

Expected:
- `InboxRedeliveryTests`:
  - with the policy, one consume and no fault;
  - without it, one fault naming `AK_InboxState_MessageId_ConsumerId`.
- `TransientRetryTests`: the inbox's `23505` is transient, and any other `23505` is not.
- `fault.sh broker` exits 0 with every `_error` queue empty.
