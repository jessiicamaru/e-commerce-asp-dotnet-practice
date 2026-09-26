# Quickstart: Validating the audit gaps and the reuse rule

> Written on 2026-09-27, after the feature merged (#141), from the code at that merge, the pull request and
> [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md).

**Feature**: [spec.md](./spec.md) | **Contracts**: [messages.md](./contracts/messages.md), [http-api.md](./contracts/http-api.md)

Each scenario maps to a requirement or success criterion. Whether these HTTP scenarios were run by hand before
the merge is not recorded; the automated tests (scenario 1) were, and the pull request reports them passing.

---

## Prerequisites

```bash
cd server
docker compose up -d          # PostgreSQL per service, RabbitMQ, Seq, Mailpit, SeaweedFS
./start-dev.sh                # or start-dev.ps1: every service and the gateway on :5000
```

Tokens (the seeded administrator, and a customer you register):

```bash
ADMIN=$(curl -fsS -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$ADMIN_EMAIL"'","password":"'"$ADMIN_PASSWORD"'"}' | jq -r .token)

EMAIL="audit-$RANDOM@example.test"
curl -fsS -X POST http://localhost:5000/api/auth/register -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23","firstName":"Lan","lastName":"Pham"}' >/dev/null
# Sign in, keeping the refresh cookie from the Set-Cookie header (it is HttpOnly and not in the body)
LOGIN=$(curl -si -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23"}')
CUSTOMER=$(echo "$LOGIN" | tail -1 | jq -r .token)
USER_ID=$(echo "$LOGIN" | tail -1 | jq -r .id)
REFRESH1=$(echo "$LOGIN" | grep -i '^set-cookie: refreshToken=' | sed -E 's/.*refreshToken=([^;]+).*/\1/')
```

---

## Scenario 1 - The automated checks (SC-001 to SC-004)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Catalog.Tests \
  --filter "FullyQualifiedName~AuditTests.Translating_a_category_and_removing_translations_are_recorded"
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests \
  --filter "FullyQualifiedName~AuditTests|FullyQualifiedName~RefreshTokenReuseTests"
```

**Expected**: all pass. The new ones are `Signing_out_is_recorded_as_the_person_and_nothing_is_recorded_for_nothing`,
`Changing_the_default_address_is_recorded_without_the_address`, `Detected_reuse_is_on_the_record` and
`A_stale_tab_from_before_a_lock_does_not_end_the_session_after_the_unlock`. At the merge the whole suites were
Identity 83/83 and Catalog 161/161.

---

## Scenario 2 - A category translated and untranslated (FR-001, FR-002)

```bash
CAT=$(curl -fsS http://localhost:5000/api/categories -H "Authorization: Bearer $ADMIN" | jq -r '.[0].id // .items[0].id')
curl -fsS -X PUT "http://localhost:5000/api/categories/$CAT/translations/en" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"name":"Mirrorless cameras","description":null}'
curl -fsS -X DELETE "http://localhost:5000/api/categories/$CAT/translations/en" -H "Authorization: Bearer $ADMIN"
curl -fsS "http://localhost:5000/api/audit?subjectId=$CAT" -H "Authorization: Bearer $ADMIN" | jq '.items[].action'
```

**Expected**: `CategoryTranslationRemoved` and `CategoryTranslated` at the top, both category `Catalog`. Removing
a product's translation (`DELETE /api/products/{id}/translations/en`) shows `ProductTranslationRemoved` the same
way (FR-003).

---

## Scenario 3 - A default address changed, with no address in the entry (FR-004)

```bash
for street in "12 Ly Thuong Kiet" "1 Trang Tien"; do
  curl -fsS -X POST http://localhost:5000/api/addresses -H "Authorization: Bearer $CUSTOMER" -H 'Content-Type: application/json' \
    -d '{"recipientName":"Lan Pham","line1":"'"$street"'","city":"Ha Noi","postalCode":"100000","country":"VN"}' | jq -r .id
done   # note the second id as ADDR2
curl -fsS -X PUT "http://localhost:5000/api/addresses/$ADDR2/default" -H "Authorization: Bearer $CUSTOMER"
curl -fsS "http://localhost:5000/api/audit?action=DefaultAddressChanged&subjectId=$ADDR2" -H "Authorization: Bearer $ADMIN"
```

**Expected**: one entry, category `User`, whose summary is "Chose another default delivery address" and which
contains neither street. Choosing `$ADDR2` again adds nothing.

---

## Scenario 4 - A stale tab after lock and unlock ends nothing (US2, SC-003)

```bash
curl -fsS -X POST "http://localhost:5000/api/users/$USER_ID/lock" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"days":1,"reason":"Cooling off"}'
curl -fsS -X POST "http://localhost:5000/api/users/$USER_ID/unlock" -H "Authorization: Bearer $ADMIN"
LOGIN2=$(curl -si -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23"}')
REFRESH2=$(echo "$LOGIN2" | grep -i '^set-cookie: refreshToken=' | sed -E 's/.*refreshToken=([^;]+).*/\1/')

curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/auth/refresh -H "Cookie: refreshToken=$REFRESH1"
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/auth/refresh -H "Cookie: refreshToken=$REFRESH2"
```

**Expected**: `401` for the stale tab, then `200` for the new session. Seq shows an Information line "... (not
rotated: a lock, a ban or a sign-out elsewhere) was presented again; refused." and no reuse warning. Before
#141 the second call was `401` too.

---

## Scenario 5 - Real reuse is on the record (FR-007, FR-008)

Refresh once with a session's cookie (which rotates it), wait more than 10 seconds, then present the **old** value
again: `401`, a warning "Refresh token reuse for user ...", every session of the user revoked, and
`GET /api/audit?action=SessionReuseDetected` shows the entry with the person as actor.

```sql
-- Identity, localhost:5435, ecommerce_identity_db: nothing active is left for the user
SELECT count(*) FROM refresh_tokens WHERE "UserId" = '<user id>' AND "RevokedAt" IS NULL;   -- 0
```

---

## Scenario 6 - Signing out is recorded, and signing out of nothing is not (FR-005)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/auth/logout -H "Cookie: refreshToken=$REFRESH2"
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5000/api/auth/logout -H "Cookie: refreshToken=not-a-token"
curl -fsS "http://localhost:5000/api/audit?action=SignedOut&actorId=$USER_ID" -H "Authorization: Bearer $ADMIN" | jq '.items | length'
```

**Expected**: both sign-outs succeed quietly; exactly one `SignedOut` entry for the user, with the user as actor and
no token in it.
