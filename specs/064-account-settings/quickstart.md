# Quickstart: Validating account settings

> Written on 2026-09-27, after the feature merged (#147), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

Every scenario below was run for the pull request; its recorded results are quoted.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

---

## Scenario 1 - The tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~AccountTests"
cd ../client && npm test -- src/pages/account
```

**Expected**: 9 server tests pass (`I_read_and_change_my_own_details`, `An_empty_name_is_refused_and_nothing_changes`,
`A_change_of_details_is_on_the_record_with_before_and_after`, `A_new_password_keeps_this_session_and_ends_every_other`,
`Without_a_session_to_keep_every_session_ends`, `A_wrong_current_password_is_refused_and_nothing_changes`,
`A_new_password_follows_the_registration_rules`, `Wrong_current_passwords_count_toward_the_sign_in_pause`,
`A_change_of_password_is_on_the_record_without_it`) and 6 page tests. At the merge: Identity 136/136, client
329/329, `tsc -b` and oxlint clean.

---

## Scenario 2 - Two browsers, end to end (US1, US2, SC-001)

Two cookie jars stand for two browsers. The refresh cookie is `Secure`; recent curl versions still send it to
`localhost` over plain HTTP (if yours does not, pass it with `-H "Cookie: refreshToken=..."` from the jar).

```bash
EMAIL="e2e-064-$RANDOM@demo.test"
curl -s -o /dev/null -X POST http://localhost:5000/api/auth/register -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23","firstName":"Lan","lastName":"E2E"}'
A=$(curl -fsS -c a.jar -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23"}' | jq -r .token)
curl -fsS -c b.jar -o /dev/null -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23"}'

curl -fsS http://localhost:5000/api/auth/me -H "Authorization: Bearer $A"; echo
curl -fsS -X PUT http://localhost:5000/api/auth/me -H "Authorization: Bearer $A" -H 'Content-Type: application/json' \
  -d '{"firstName":"Mai","lastName":"E2E","phone":"0912 345 678"}'; echo
curl -s -o /dev/null -w 'wrong current password: %{http_code}\n' -X PUT http://localhost:5000/api/auth/me/password \
  -H "Authorization: Bearer $A" -H 'Content-Type: application/json' -d '{"currentPassword":"nope","newPassword":"NewPassw0rd!45"}'
curl -s -o /dev/null -w 'change password (browser A): %{http_code}\n' -b a.jar -X PUT http://localhost:5000/api/auth/me/password \
  -H "Authorization: Bearer $A" -H 'Content-Type: application/json' -d '{"currentPassword":"Passw0rd!23","newPassword":"NewPassw0rd!45"}'
curl -s -o /dev/null -w 'browser A refresh: %{http_code}\n' -b a.jar -X POST http://localhost:5000/api/auth/refresh
curl -s -o /dev/null -w 'browser B refresh: %{http_code}\n' -b b.jar -X POST http://localhost:5000/api/auth/refresh
curl -s -o /dev/null -w 'me without a token: %{http_code}\n' http://localhost:5000/api/auth/me
```

**Expected** - the pull request's run printed:

```text
me: {"email":"e2e-064-…@demo.test","firstName":"Lan","lastName":"E2E","phone":null,"emailConfirmed":false}
update me: {…"firstName":"Mai",…"phone":"0912 345 678"…}
wrong current password: 400
change password (browser A): 204
browser A refresh: 200
browser B refresh: 401
old password: 401
new password: signed in as Mai E2E
me without a token: 401
```

```sql
-- Identity, localhost:5435, ecommerce_identity_db: one active session left for the person
SELECT count(*) FROM refresh_tokens t JOIN users u ON u."Id" = t."UserId"
 WHERE u."Email" = '<the address>' AND t."RevokedAt" IS NULL;
```

---

## Scenario 3 - Bruno

```bash
cd bruno
npx @usebruno/cli run auth --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: the `auth` folder passes (20/20 in the pull request's run, after one of the new requests was fixed to
compare with the run's own customer; the full collection had run 334/335 before that fix).

---

## Mutations (Principle V)

| Mutation | Caught by |
| :-- | :-- |
| current password not checked | `A_wrong_current_password_is_refused…`, `Wrong_current_passwords_count…` |
| wrong current password not counted | `Wrong_current_passwords_count_toward_the_sign_in_pause` |
| no other session ends | `A_new_password_keeps_this_session…`, `Without_a_session_to_keep…` |
| this session ends too | `A_new_password_keeps_this_session_and_ends_every_other` |
| no "before" in the profile diff | `A_change_of_details_is_on_the_record_with_before_and_after` |
