# Quickstart: Validating the re-lock rule

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d
```

For the end-to-end scenarios, Identity rebuilt from this branch (containers or `start-dev`), the seeded
administrator, a moderator and a customer: `ADMIN`, `MOD` hold their tokens, `CUSTOMER_ID` the customer's id.

## Scenario 1 - The server tests (US1, SC-001 to SC-003)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ModerationTests"
```

**Expected**: green, including `A_moderator_does_not_shorten_a_lock_by_locking_again`,
`A_moderator_extends_a_lock_and_shortens_only_one_they_could_have_lifted` and `An_administrator_shortens_any_lock`.

## Scenario 2 - Through the gateway (SC-004)

```bash
curl -fsS -X POST "http://localhost:5000/api/users/$CUSTOMER_ID/lock" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"days":300,"reason":"An administrator decided"}' | jq .lockedUntil
curl -s -X POST "http://localhost:5000/api/users/$CUSTOMER_ID/lock" -H "Authorization: Bearer $MOD" \
  -H 'Content-Type: application/json' -d '{"days":1,"reason":"Shorter"}' | jq .status,.detail
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/users/$CUSTOMER_ID/unlock" -H "Authorization: Bearer $ADMIN"
```

**Expected**: a date about 300 days away; `403` and "Only an administrator can shorten a lock with more than 30
days to run."; `200` (cleanup).

## Scenario 3 - Bruno

Run the collection (CLAUDE.md, "Bruno collection"). **Expected**: `admin-users` passes, including
`a moderator cannot shorten the lock by locking again` (403).

## Scenario 4 - Mutations (SC-005)

Each, then restore (and `touch` the file so MSBuild rebuilds):

| Mutation in `UserAdministration.cs` | Expected red |
| :-- | :-- |
| Remove the `EnsureMayShorten` call | `A_moderator_does_not_shorten_a_lock_by_locking_again` |
| Drop `caller.IsInRole(RoleNames.Admin) \|\|` from the early return | `An_administrator_shortens_any_lock` |
| Drop the reach condition (always refuse a sooner end) | `A_moderator_extends_a_lock_and_shortens_only_one_they_could_have_lifted` |
