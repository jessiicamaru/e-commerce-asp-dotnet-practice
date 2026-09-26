# Quickstart: Validating the sign-in refusal

> Written on 2026-09-27, after the feature merged (#130), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/http-api.md](contracts/http-api.md)

## Prerequisites

```bash
cd server
docker compose up -d                                  # Identity's database on 5435, RabbitMQ
./start-dev.sh                                        # or the containers: -f docker-compose.app.yml
```

`ADMIN_EMAIL` / `ADMIN_PASSWORD` set for the seeded administrator. For the client tests, `npm ci` in
`client/`.

## Scenario 1 - The facts are on the exception (US2, FR-002)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ModerationTests"
```

**Expected**: green. The lock test asserts `code = AccountLocked`, `reason`, and an `until` of kind UTC equal
to `LockedUntil` within a millisecond (PostgreSQL keeps microseconds); the ban test asserts `AccountBanned`,
the reason, and no `until`; a wrong password on the locked account is still `UnauthorizedAccessException`.

## Scenario 2 - The facts reach the body in Production (US2 scenario 2, FR-001, FR-005)

```bash
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ForbiddenProblemTests"
```

**Expected**: three tests green - facts beside the sentence outside Development, `until` as
`2026-10-01T07:30:00Z`, and a fact named `traceId` unable to replace the real one.

## Scenario 3 - End to end through the gateway (SC-003)

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)

# a throwaway customer
curl -fsS -X POST http://localhost:5000/api/auth/register -H 'Content-Type: application/json' \
  -d '{"email":"e2e-049@demo.test","password":"Right-Passw0rd","firstName":"E","lastName":"Two"}'
ID=$(curl -fsS "http://localhost:5000/api/users?search=e2e-049" -H "Authorization: Bearer $ADMIN" | jq -r '.items[0].id')

curl -fsS -X POST "http://localhost:5000/api/users/$ID/lock" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"days":3,"reason":"Spam in reviews"}'

curl -s -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"e2e-049@demo.test","password":"Right-Passw0rd"}' | jq       # right password
curl -s -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"e2e-049@demo.test","password":"Wrong-Passw0rd"}' | jq       # wrong password
```

**Expected**: the right password gives 403 with `code: "AccountLocked"`, an `until` ending in `Z`, and
`reason: "Spam in reviews"`; the wrong password gives the same bare 401 as for any account. Ban the account
(`POST /api/users/$ID/ban` with `{"reason":"Fraud"}`) and sign in again: 403 with `code: "AccountBanned"`,
`reason`, and no `until`.

The pull request ran these three cases against a rebuilt Identity container and recorded them (see
[contracts/http-api.md](contracts/http-api.md)); its throwaway account was left banned.

## Scenario 4 - Bruno (SC-003)

Run the whole collection from `bruno/` (`npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL"
--env-var "adminPassword=$ADMIN_PASSWORD"`); the `admin-users` folder relies on the customer the earlier folders
register.
**Expected**: `the locked customer cannot sign in` passes its "the facts come beside the sentence" test -
`code` is `AccountLocked`, `reason` contains "Bruno checks the lock", `until` is in the future and ends in `Z`.

## Scenario 5 - The page words it (US1, SC-001)

```bash
cd client
npx vitest run src/pages/sign-in
```

**Expected**: the eight tests of `SignInPage refusals (specs/049)` pass - locked in English and Vietnamese
(the local time, never "UTC"), banned in both, a wrong password says only "wrong", an unknown 403 shows its
own sentence, a 500 and a network failure show the generic sentence.

By hand: sign in on the storefront as the locked account above with the right password, switch the language,
and read the alert.
