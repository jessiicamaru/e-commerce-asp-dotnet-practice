# Quickstart: Validating the password reset

> Written on 2026-09-27, after the feature merged (#144), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

Scenarios 1 to 4 were run for the pull request; its recorded output is quoted.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # includes Mailpit on :8025
```

---

## Scenario 1 - The server tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~PasswordResetTests"
```

**Expected**: 8 pass - `Asking_answers_the_same_for_anybody_and_only_a_real_account_gets_a_link`,
`Only_the_hash_is_kept_and_the_link_carries_the_token`, `A_reset_changes_the_password_and_ends_every_session`,
`A_link_works_once_and_not_after_it_expires_and_a_made_up_one_never_does`, `Asking_again_replaces_the_earlier_link`,
`Two_submissions_of_one_link_at_once_reset_once`, `A_new_password_follows_the_registration_rules`,
`Both_steps_are_on_the_record_with_no_secret_in_them`. At the merge the Identity suite was 101/101.

## Scenario 2 - The storefront tests

```bash
cd client
npm test -- src/pages/forgot-password src/pages/reset-password
```

**Expected**: pass, including "says the same thing whether or not the address has an account", "sends nothing
when the two passwords differ" and "offers a new link when the server refuses the token". At the merge: 300/300,
`tsc -b` and oxlint clean.

## Scenario 3 - Bruno

```bash
cd bruno
npx @usebruno/cli run "auth/forgot password is 202 for anybody.yml" --env local
npx @usebruno/cli run "security-checks/reset with a made-up token is 400.yml" --env local
```

**Expected**: both pass.

---

## Scenario 4 - End to end through Mailpit (US1, US2, SC-001, SC-002)

```bash
EMAIL="reset-$RANDOM@example.test"
curl -s -o /dev/null -w 'register: %{http_code}\n' -X POST http://localhost:5000/api/auth/register \
  -H 'Content-Type: application/json' -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23","firstName":"Lan","lastName":"Pham"}'
LOGIN=$(curl -si -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23"}')
OLD_REFRESH=$(echo "$LOGIN" | grep -i '^set-cookie: refreshToken=' | sed -E 's/.*refreshToken=([^;]+).*/\1/')

curl -s -o /dev/null -w 'forgot (real, en): %{http_code}\n' -X POST http://localhost:5000/api/auth/forgot-password \
  -H 'Accept-Language: en' -H 'Content-Type: application/json' -d '{"email":"'"$EMAIL"'"}'
curl -s -o /dev/null -w 'forgot (unknown): %{http_code}\n' -X POST http://localhost:5000/api/auth/forgot-password \
  -H 'Content-Type: application/json' -d '{"email":"nobody@example.test"}'

# Read the link from Mailpit's API (http://localhost:8025) once the dispatcher has sent it
TOKEN=$(curl -fsS "http://localhost:8025/api/v1/search?query=to:$EMAIL" | jq -r '.messages[0].ID' \
  | xargs -I{} curl -fsS http://localhost:8025/api/v1/message/{} | jq -r .Text | grep -o 'token=[^ ]*' | cut -d= -f2)

curl -s -o /dev/null -w 'reset: %{http_code}\n' -X POST http://localhost:5000/api/auth/reset-password \
  -H 'Content-Type: application/json' -d '{"token":"'"$TOKEN"'","password":"NewPassw0rd!45"}'
curl -s -o /dev/null -w 'reset again (used): %{http_code}\n' -X POST http://localhost:5000/api/auth/reset-password \
  -H 'Content-Type: application/json' -d '{"token":"'"$TOKEN"'","password":"NewPassw0rd!45"}'
curl -s -o /dev/null -w 'old session refresh: %{http_code}\n' -X POST http://localhost:5000/api/auth/refresh \
  -H "Cookie: refreshToken=$OLD_REFRESH"
```

**Expected** - the pull request's run printed:

```text
register: 200
login old: 200
forgot (real, en):   202
forgot (unknown):    202
subject: Reset your password
link host+path: http://localhost:8088/reset-password
reset:               204
reset again (used):  400
login old password:  401
login new password:  200
old session refresh: 401
storefront link:     200
outgoing_emails row: Sent data={} lang=en
password_reset_tokens: 1 row, used, hash length 64
```

The last two lines come from Identity's database:

```sql
-- localhost:5435, ecommerce_identity_db
SELECT "Status", "DataJson", "Language" FROM outgoing_emails WHERE "Template" = 'PasswordReset' ORDER BY "CreatedAt" DESC LIMIT 1;
SELECT "UsedAt" IS NOT NULL AS used, length("TokenHash") FROM password_reset_tokens ORDER BY "CreatedAt" DESC LIMIT 1;
```

---

## Scenario 5 - Mutations (Principle V)

Each was applied, observed red, reverted, and the files touched so MSBuild rebuilt:

| Mutation | Caught by |
| :-- | :-- |
| drop `"UsedAt" IS NULL` from the claim | `A_link_works_once…` and `Two_submissions_of_one_link_at_once_reset_once` |
| drop `RevokeAllRefreshTokensAsync` | `A_reset_changes_the_password_and_ends_every_session` |
| drop the scrub | `Only_the_hash_is_kept_and_the_link_carries_the_token` |
| drop `DeleteUnusedAsync` | `Asking_again_replaces_the_earlier_link` |
| client: no password-mismatch check | `sends nothing when the two passwords differ` |
| client: token refusal not recognised | `offers a new link when the server refuses the token` |
