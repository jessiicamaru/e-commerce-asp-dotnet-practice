# Quickstart: Validating "signed in unless it says otherwise"

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d
```

## Scenario 1 - The fallback through a real pipeline (US1, SC-001)

```bash
cd server
dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~FallbackPolicyTests"
```

**Expected**: 3 passed - says-nothing is 401 anonymous and 200 signed in; the public endpoint is 200 anonymous. No
database needed.

## Scenario 2 - Every action declares its access (US3, SC-002)

```bash
cd server
for s in Identity Catalog Order Inventory Payment Cart Activity; do
  dotnet test tests/Ecommerce.$s.Tests --filter "FullyQualifiedName~EndpointAccessTests"
done
```

**Expected**: all pass (Identity's has two tests: its own controllers, and the helper against a controller that
says nothing).

## Scenario 3 - Nothing public stopped being public (US2, SC-003)

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build --no-deps identity catalog order inventory payment cart activity
docker ps --format '{{.Names}}\t{{.Status}}'          # every service (healthy): /health is still anonymous
cd ../bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: every container healthy; the collection passes in full.

## Scenario 4 - Checkout still prices over gRPC (US2 scenario 3, SC-004)

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expected**: the order reaches a terminal state with the stock moved by exactly the amount ordered - pricing reached
Catalog's anonymous gRPC service without a token.

## Scenario 5 - By hand

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/auth/logout      # 204
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/api/auth/me                   # 401
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5056/health                        # 200
```

## Scenario 6 - Mutations (SC-005)

| Mutation | Expected red |
| :-- | :-- |
| `services.AddAuthorization()` without the fallback | `FallbackPolicyTests.An_endpoint_that_says_nothing_refuses_an_anonymous_caller` |
| Remove `[Authorize]` from `AuthController`'s class | Identity `EndpointAccessTests.Every_action_says_who_may_call_it` |
| `EndpointAccess` ignores the controller's attribute | both Identity `EndpointAccessTests` |
