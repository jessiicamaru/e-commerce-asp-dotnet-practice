# Quickstart: Validating Moderators, locks and bans

> Written on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Feature**: [spec.md](./spec.md) | **Contracts**: [http-api.md](./contracts/http-api.md), [messages.md](./contracts/messages.md)

How to prove the feature works. Each scenario names the story or criterion it checks. The expected results
are those of the code at #95; where a later spec changed one, the scenario says so. Which of these
scenarios were run by hand for #95 is not recorded beyond the pull request's evidence (tests, Bruno,
`verify-saga.sh`, screenshots).

---

## Prerequisites

```bash
cd server
docker compose up -d
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ \
                          --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/
./start-dev.sh          # or ./start-dev.ps1; Identity on 5056, the gateway on 5000, Activity on 5063
```

Identity seeds the `Moderator` role at startup. The audit log (scenario 7) needs Activity and RabbitMQ.

Helpers used below (Git Bash, `jq` installed):

```bash
GW=http://localhost:5000
login() {  # prints the access token; $1 email, $2 password
  curl -sS -X POST $GW/api/auth/login -H 'Content-Type: application/json' \
    -d '{"email":"'"$1"'","password":"'"$2"'"}' | jq -r .token
}
ADMIN=$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
PW='Passw0rd!23'
MOD_EMAIL="mod-$(date +%s)@example.test";  CUS_EMAIL="cus-$(date +%s)@example.test"
for e in "$MOD_EMAIL" "$CUS_EMAIL"; do
  curl -sS -X POST $GW/api/auth/register -H 'Content-Type: application/json' \
    -d '{"email":"'"$e"'","password":"'"$PW"'","firstName":"Test","lastName":"Person"}' | jq -c .roles
done
```

**Expected**: `["Customer"]` twice - self-registration never grants more.

---

## Scenario 1 - Staff find a person by part of their email (FR-002)

```bash
curl -sS "$GW/api/users?search=${MOD_EMAIL:0:12}" -H "Authorization: Bearer $ADMIN" | jq '.totalCount, .items[0].email'
curl -sS -o /dev/null -w '%{http_code}\n' "$GW/api/users"                                    # no token
curl -sS -o /dev/null -w '%{http_code}\n' "$GW/api/users" -H "Authorization: Bearer $(login "$CUS_EMAIL" "$PW")"
```

**Expected**: at least one match including the new person, with `lockedUntil` and `bannedAt` null; then
**401** without a token and **403** for a customer.

---

## Scenario 2 - An administrator grants Moderator, and it arrives at the next sign-in or refresh (US1, SC-001)

```bash
MOD_ID=$(curl -sS "$GW/api/users?search=$MOD_EMAIL" -H "Authorization: Bearer $ADMIN" | jq -r '.items[0].id')
curl -sS -X PUT "$GW/api/users/$MOD_ID/roles/Moderator" -H "Authorization: Bearer $ADMIN" | jq -c .roles
curl -sS -X POST $GW/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$MOD_EMAIL"'","password":"'"$PW"'"}' | jq -c .roles
MOD=$(login "$MOD_EMAIL" "$PW")
```

**Expected**: `["Customer","Moderator"]` from the grant, and the same roles on the new sign-in.

Then the negative cases:

```bash
curl -sS -X PUT "$GW/api/users/$MOD_ID/roles/Admin"     -H "Authorization: Bearer $ADMIN" | jq .status   # 400
curl -sS -o /dev/null -w '%{http_code}\n' -X PUT "$GW/api/users/$MOD_ID/roles/Moderator" -H "Authorization: Bearer $MOD"   # 403
```

**Expected**: 400 ("Only the Moderator role can be granted here.") and 403. The granted person has a
`ModeratorGranted` notification: `curl -sS $GW/api/notifications -H "Authorization: Bearer $MOD"`.

---

## Scenario 3 - A moderator locks a customer, who cannot get back in (US2, SC-002, FR-005, FR-009)

Sign the customer in first and keep the refresh cookie, so the refresh can be tried after the lock. The
cookie is `Secure`; pass it back as a header rather than relying on curl's cookie jar over plain HTTP.

```bash
CUS_ID=$(curl -sS "$GW/api/users?search=$CUS_EMAIL" -H "Authorization: Bearer $MOD" | jq -r '.items[0].id')
REFRESH=$(curl -sS -D - -o /dev/null -X POST $GW/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$CUS_EMAIL"'","password":"'"$PW"'"}' | sed -n 's/^[Ss]et-[Cc]ookie: refreshToken=\([^;]*\).*/\1/p')

curl -sS -X POST "$GW/api/users/$CUS_ID/lock" -H "Authorization: Bearer $MOD" \
  -H 'Content-Type: application/json' -d '{"days":3,"reason":"Spam in reviews"}' | jq '.lockedUntil, .lockReason'

curl -sS -X POST $GW/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$CUS_EMAIL"'","password":"'"$PW"'"}' | jq '.status, .detail'
curl -sS -X POST $GW/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$CUS_EMAIL"'","password":"Wrong-Passw0rd"}' | jq '.status, .detail'
curl -sS -o /dev/null -w '%{http_code}\n' -X POST $GW/api/auth/refresh -H "Cookie: refreshToken=$REFRESH"
```

**Expected**:

1. The lock answers a date three days ahead and the reason.
2. Right password: **403**, `detail` "This account is locked until … UTC: Spam in reviews". (Since
   specs/049 the body also carries `code`, `until` and `reason`.)
3. Wrong password: **401** "Invalid email or password." - nothing about the lock.
4. Refresh: **401**.

In the database, every session of the customer is revoked:

```bash
docker exec -it ecommerce-identity-db psql -U "$DB_USER" -d ecommerce_identity_db -c \
  "SELECT \"LockedUntil\", \"LockReason\", (SELECT count(*) FROM refresh_tokens t
     WHERE t.\"UserId\" = u.\"Id\" AND t.\"RevokedAt\" IS NULL) AS live_sessions
   FROM users u WHERE lower(\"Email\") = lower('$CUS_EMAIL');"
```

**Expected**: the date, the reason, and `live_sessions = 0`.

---

## Scenario 4 - The moderator's limits (US2 acceptance 2-4, FR-007, FR-008)

```bash
lock() { curl -sS -X POST "$GW/api/users/$1/lock" -H "Authorization: Bearer $2" \
  -H 'Content-Type: application/json' -d '{"days":'"$3"',"reason":"test"}' | jq -c '[.status, .detail]'; }
ADMIN_ID=$(curl -sS "$GW/api/users?search=$ADMIN_EMAIL" -H "Authorization: Bearer $ADMIN" | jq -r '.items[0].id')

lock "$CUS_ID"   "$MOD" 31          # 403 "A moderator can lock an account for at most 30 days."
lock "$MOD_ID"   "$MOD" 1           # 409 "You cannot lock or ban your own account."
lock "$ADMIN_ID" "$MOD" 1           # 409 "An administrator cannot be locked or banned."
lock "$CUS_ID"   "$ADMIN" 366       # 400 - outside 1 to 365 for anybody
curl -sS -o /dev/null -w '%{http_code}\n' -X POST "$GW/api/users/$CUS_ID/ban" -H "Authorization: Bearer $MOD" \
  -H 'Content-Type: application/json' -d '{"reason":"no"}'     # 403 - administrators only
```

A moderator on another moderator (403 "Only an administrator can lock a moderator.") needs a second
moderator; `ModerationTests` covers it (scenario 8).

---

## Scenario 5 - Unlocking restores access (US2 acceptance 1)

```bash
curl -sS -X POST "$GW/api/users/$CUS_ID/unlock" -H "Authorization: Bearer $MOD" | jq '.lockedUntil'
login "$CUS_EMAIL" "$PW" | head -c 20; echo
```

**Expected**: `null`, then a token. At #95 a moderator could unlock any locked account; since specs/050 a
moderator lifts only a lock with at most 30 days still to run, never their own and never a moderator's.

---

## Scenario 6 - A ban holds until an administrator lifts it (US2 acceptance 3 and 6)

```bash
curl -sS -X POST "$GW/api/users/$CUS_ID/ban" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"reason":"Fraud"}' | jq '.bannedAt, .banReason'
curl -sS -X POST $GW/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$CUS_EMAIL"'","password":"'"$PW"'"}' | jq '.status, .detail'      # 403 "This account is banned: Fraud"
curl -sS -X POST "$GW/api/users/$CUS_ID/unlock" -H "Authorization: Bearer $MOD" > /dev/null
curl -sS -X POST $GW/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$CUS_EMAIL"'","password":"'"$PW"'"}' | jq '.status'                # still 403
curl -sS -X PUT "$GW/api/users/$CUS_ID/roles/Moderator" -H "Authorization: Bearer $ADMIN" | jq '.status'   # 409
curl -sS -X POST "$GW/api/users/$CUS_ID/unban" -H "Authorization: Bearer $ADMIN" | jq '.bannedAt'           # null
login "$CUS_EMAIL" "$PW" | head -c 20; echo
```

**Expected**: banned with the reason; sign-in 403; unlocking does not lift it; a grant to a banned account
is 409; after the lift, sign-in works.

---

## Scenario 7 - Every action is on the record (US3, SC-003)

```bash
curl -sS "$GW/api/audit?category=Moderation&subjectType=User&subjectId=$CUS_ID" -H "Authorization: Bearer $ADMIN" \
  | jq -c '.items[] | [.action, .actorRole]'
curl -sS "$GW/api/audit?category=Security&subjectType=User&subjectId=$MOD_ID" -H "Authorization: Bearer $ADMIN" \
  | jq -c '.items[] | [.action, .actorRole]'
```

**Expected**: under Moderation for the customer, `AccountLocked` and `AccountUnlocked` by `Moderator`, then
`AccountBanned` and `BanLifted` by `Admin` (and the refused lock attempts recorded nothing); under
Security, `RoleGranted` by `Admin`. The customer's refused sign-ins appear under Security as
`SignInRefused`. Each entry's before and after differ in exactly the field that changed.

---

## Scenario 8 - The automated tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~ModerationTests"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests
```

**Expected at #95**: the 8 `ModerationTests` pass, and the whole Identity suite reports 65/65 (the pull
request's figure). They run against the real PostgreSQL on 5435. Later specs added cases to
`ModerationTests` (specs/049, specs/050), so today's counts are higher.

```bash
cd client
npx vitest run src/pages/admin-users src/layouts/admin-layout src/components/auth/require-role
```

**Expected**: the 7 `AdminUsersPage (specs/043)` tests, the 2 `AdminLayout (specs/043)` tests and the
`RequireRole` tests pass. At #95 the whole client suite was 195/195.

---

## Scenario 9 - Bruno, end to end through the gateway

```bash
cd bruno
export ADMIN_EMAIL=... ADMIN_PASSWORD=...
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

Run the whole collection: `admin-users/` (folder `seq: 12`) reads `customerEmail`, `customerPassword` and
`adminToken` set by earlier folders. Its 15 requests: register a future moderator; an administrator finds
them by email; a customer cannot list people (403); an administrator grants Moderator; the moderator signs
in holding the role; a moderator finds the customer; cannot grant roles (403); cannot lock for more than 30
days (403, detail names "30 days"); locks the customer for 3 days; the locked customer cannot sign in (403
with the reason); a moderator cannot ban (403); unlocks; the customer signs in again; an administrator
revokes Moderator; the audit log records exactly one lock and one unlock, each by a Moderator.
`security-checks/people without a token is 401.yml` adds the 401.

**Expected at #95**: 148/148 requests, 238 tests (the pull request's figures).

---

## Scenario 10 - The console, by role (US4)

Open the storefront, sign in as the moderator from scenario 2, and go to `/admin`.

**Expected**: redirected to `/admin/users`; the sidebar shows Users only. A customer's row menu offers Lock
(and Unlock when locked) but no Grant, Revoke or Ban; Lock is disabled on the moderator's own row and on
the administrator's. The lock dialog offers 1, 3, 7, 14 and 30 days and will not submit without a reason.
Signed in as the administrator, the sidebar shows Fulfilment, Payouts, Users and Audit, and the dialog also
offers 90 and 365 days. At 390px wide the page has no horizontal overflow (#95's screenshot).
