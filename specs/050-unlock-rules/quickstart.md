# Quickstart: Validating the unlock rules

> Written on 2026-09-27, after the feature merged (#131), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d
./start-dev.sh                     # or the containers
```

The seeded administrator (`ADMIN_EMAIL` / `ADMIN_PASSWORD`), a moderator account (register one and grant it
`PUT /api/users/{id}/roles/Moderator` as the administrator) and a customer. `ADMIN`, `MOD` hold their tokens,
`MOD_ID` and `CUSTOMER_ID` their ids.

## Scenario 1 - The server tests (US1, SC-001, SC-002)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ModerationTests"
```

**Expected**: green, including `Nobody_unlocks_themselves_even_with_a_token_that_outlived_the_lock`,
`Only_an_administrator_unlocks_a_moderator` and `A_moderator_lifts_only_a_lock_they_could_have_set` - the last
also asserts that the refused unlock published no audit entry and left `LockedUntil` set.

## Scenario 2 - End to end through the gateway (SC-003)

The four steps the pull request ran against a rebuilt Identity container:

```bash
# 1. a moderator unlocks their own account
curl -s -X POST "http://localhost:5000/api/users/$MOD_ID/unlock" -H "Authorization: Bearer $MOD" | jq .status,.detail
# 2. an administrator locks a customer for 365 days; the moderator tries to unlock
curl -fsS -X POST "http://localhost:5000/api/users/$CUSTOMER_ID/lock" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"days":365,"reason":"An administrator decided"}'
curl -s -X POST "http://localhost:5000/api/users/$CUSTOMER_ID/unlock" -H "Authorization: Bearer $MOD" | jq .status,.detail
# 3. the administrator unlocks it
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/users/$CUSTOMER_ID/unlock" -H "Authorization: Bearer $ADMIN"
# 4. the moderator locks the customer for 7 days, then unlocks
curl -fsS -X POST "http://localhost:5000/api/users/$CUSTOMER_ID/lock" -H "Authorization: Bearer $MOD" \
  -H 'Content-Type: application/json' -d '{"days":7,"reason":"Cooling off"}'
curl -s -o /dev/null -w '%{http_code}\n' -X POST "http://localhost:5000/api/users/$CUSTOMER_ID/unlock" -H "Authorization: Bearer $MOD"
```

**Expected**: `409 You cannot unlock your own account.`; `403 Only an administrator can lift a lock with more
than 30 days to run.`; `200`; `200`.

## Scenario 3 - Bruno

Run the whole collection from `bruno/` (see CLAUDE.md for the admin credentials). **Expected**:
`admin-users/a moderator cannot unlock their own account` passes (409, "your own account").

## Scenario 4 - The page (US2)

```bash
cd client
npx vitest run src/pages/admin-users
```

**Expected**: `does not offer a moderator to unlock a moderator, themselves, or a lock longer than theirs` and
`offers an administrator to unlock anybody but themselves` pass.
