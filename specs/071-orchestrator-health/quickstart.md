# Quickstart: Validating the Orchestrator's /health

> Written on 2026-09-27, after the feature merged (#155), from the code at that merge, the pull request and
> docs/architecture/microservices-design.md.

**Feature**: [spec.md](spec.md) | **Contract**: [http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build orchestrator gateway
```

---

## Scenario 1 - The tests (SC-001)

```bash
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Orchestrator.Tests --filter "FullyQualifiedName~HealthTests"
DB_PASSWORD=<password> dotnet test tests/Ecommerce.Orchestrator.Tests        # PR: 17/17
```

**Expected**: `Health_is_200_and_names_the_saga_database_and_the_broker` (PostgreSQL on 5436) and
`Health_is_503_when_the_saga_database_cannot_be_reached` pass.

---

## Scenario 2 - Up, stopped, restarted through the gateway (US1, SC-002)

```bash
curl -s -w '\n%{http_code}\n' http://localhost:5000/api/orchestrator/health
docker compose -f docker-compose.yml -f docker-compose.app.yml stop orchestrator
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/orchestrator/health
docker compose -f docker-compose.yml -f docker-compose.app.yml start orchestrator
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/orchestrator/health
docker inspect --format '{{.State.Health.Status}}' $(docker compose -f docker-compose.yml -f docker-compose.app.yml ps -q orchestrator)
```

**Expected** (the PR's run): `200 {"status":"Healthy","service":"Orchestrator","checks":[masstransit-bus Healthy,
orchestrator_postgres_db Healthy]}`; stopped `502`; restarted `200`, and docker reports `healthy`.

---

## Scenario 3 - Everything waits for it (US2, SC-003)

```bash
docker compose -f docker-compose.yml -f docker-compose.app.yml ps       # orchestrator (healthy); gateway started after it
cd server && ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: `verify-saga.sh` probes the Orchestrator with the other services and passes (`approve=pass` in the PR,
including its cancellation checks). In CI, the `saga-e2e` job prints "All seven services are healthy, the orchestrator
included."
