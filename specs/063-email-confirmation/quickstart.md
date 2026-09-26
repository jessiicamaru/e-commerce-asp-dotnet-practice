# Quickstart: Validating email confirmation

> Written on 2026-09-27, after the feature merged (#146), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

Every scenario below was run for the pull request; its recorded results are quoted.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # Mailpit on :8025 is required
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ \
                          --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/   # when using start-dev
```

An administrator token as `$ADMIN`.

---

## Scenario 1 - The tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~EmailConfirmationTests"
cd ../client && npm test -- src/components/layout/confirm-email-banner src/pages/confirm-email src/pages/open-shop src/pages/admin-shops
```

**Expected**: all pass - 11 server tests (`Registering_sends_one_link_in_the_language_asked_for_and_the_account_is_unconfirmed`,
`The_link_confirms_the_address_and_sign_in_and_refresh_say_so`, `Two_submissions_of_one_link_at_once_confirm_once`,
`Sending_it_again_within_a_minute_sends_nothing_even_many_times_at_once`, `An_unconfirmed_customer_cannot_apply_to_sell`,
`An_application_is_approved_only_once_the_address_is_confirmed`, `The_link_is_in_the_email_and_nowhere_else_once_it_is_sent` ...).
At the merge: Identity 127/127, Gateway 12/12, client 323/323 with `tsc -b` and oxlint clean.

---

## Scenario 2 - The backfill (US4, SC-003)

```sql
-- Identity, localhost:5435, ecommerce_identity_db, right after the migration
SELECT count(*) AS confirmed, count(*) FILTER (WHERE "EmailConfirmedAt" = "CreatedAt") AS backfilled FROM users;
```

**Expected**: every existing account confirmed with `EmailConfirmedAt = CreatedAt`. The pull request's run: 32 of
32.

---

## Scenario 3 - End to end through the gateway and Mailpit (US1 to US3)

```bash
EMAIL="shop-$RANDOM@example.test"
REG=$(curl -fsS -X POST http://localhost:5000/api/auth/register-seller -H 'Accept-Language: en' -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"Passw0rd!23","firstName":"Mai","lastName":"Le","shopName":"Mai Lens '"$RANDOM"'"}')
echo "$REG" | jq '.emailConfirmed'                                  # false
APP=$(curl -fsS "http://localhost:5000/api/shop-applications?status=Pending" -H "Authorization: Bearer $ADMIN" \
  | jq -r --arg e "$EMAIL" '.items[] | select(.applicantEmail == $e) | .id')
curl -s -o /dev/null -w 'approve before confirming: %{http_code}\n' -X POST "http://localhost:5000/api/shop-applications/$APP/approve" \
  -H "Authorization: Bearer $ADMIN"

TOKEN=$(curl -fsS "http://localhost:8025/api/v1/search?query=to:$EMAIL" | jq -r '.messages[0].ID' \
  | xargs -I{} curl -fsS http://localhost:8025/api/v1/message/{} | jq -r .Text | grep -o 'confirm-email?token=[^ ]*' | cut -d= -f2)
curl -s -o /dev/null -w 'confirm: %{http_code}\n'       -X POST http://localhost:5000/api/auth/confirm-email -H 'Content-Type: application/json' -d '{"token":"'"$TOKEN"'"}'
curl -s -o /dev/null -w 'confirm again: %{http_code}\n' -X POST http://localhost:5000/api/auth/confirm-email -H 'Content-Type: application/json' -d '{"token":"'"$TOKEN"'"}'
curl -s -o /dev/null -w 'approve now: %{http_code}\n'   -X POST "http://localhost:5000/api/shop-applications/$APP/approve" -H "Authorization: Bearer $ADMIN"
```

**Expected** - the pull request's run printed:

```text
admin emailConfirmed: True
registered, emailConfirmed: False
approve before confirming: 409
resend straight away (inside the minute): 202
emails to this address: ['Confirm your email address']
confirm: 204
confirm again: 400
resend once confirmed: 409
approve now: 200
sign in: emailConfirmed=True roles=['Customer', 'Seller']
```

The two resend lines are `POST /api/auth/resend-confirmation` with the new account's bearer token, before and after
confirming.

---

## Scenario 4 - An unconfirmed customer cannot apply (FR-007)

Register a customer, then with its token `POST /api/shop-applications` with a shop name.

**Expected**: 403 with `code: "EmailNotConfirmed"`. The storefront's `/open-shop` asks them to confirm first.

---

## Scenario 5 - Bruno

```bash
cd bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: the whole collection passes - 199/199 requests, 326/326 tests in the pull request's run. The seller
folder proves "approving before the address is confirmed is 409", then reads the link from Mailpit and confirms.

---

## Mutations (Principle V)

| Mutation | Caught by |
| :-- | :-- |
| a used link works again | `Two_submissions…`, `A_link_works_once…` |
| registering sends no link | 6 tests |
| an unconfirmed customer may apply | `An_unconfirmed_customer_cannot_apply_to_sell` |
| an unconfirmed applicant may be approved | `An_application_is_approved_only_once_the_address_is_confirmed` |
| resend without the interval | `…within_a_minute_sends_nothing…` |
| the resend interval without the row lock | the same, 5 at once |
| the sent link is not scrubbed | `The_link_is_in_the_email_and_nowhere_else_once_it_is_sent` |
| sign-in always says "confirmed" | `The_link_confirms_the_address_and_sign_in_and_refresh_say_so` |
| resend keeps the old link | `Sending_it_again_replaces_the_link` |
| client: the confirm page sends twice under StrictMode | `sends the token from the link once` |
| client: `/open-shop` shows the form anyway | `asks to confirm the address first…` |
