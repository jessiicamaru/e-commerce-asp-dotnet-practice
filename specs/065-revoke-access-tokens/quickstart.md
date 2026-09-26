# Quickstart: Validating access-token revocation

> Written on 2026-09-27, after the feature merged (#148), from the code at that merge, the pull request and
> [docs/features/auth/jwt-setup.md](../../docs/features/auth/jwt-setup.md).

**Feature**: [spec.md](./spec.md) | **Contracts**: [messages.md](./contracts/messages.md), [http-api.md](./contracts/http-api.md)

Every scenario below was run for the pull request, with the seven services rebuilt; its recorded results are quoted.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build
```

An administrator token as `$ADMIN`; a registered customer with id `$USER_ID`, signed in with a cookie jar
(`-c c.jar`) and access token `$CUSTOMER`.

---

## Scenario 1 - The tests

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests --filter "FullyQualifiedName~AccessTokenRevocation"
```

**Expected**: all pass - `A_token_issued_before_the_revocation_is_refused_and_one_issued_after_is_not`,
`A_token_issued_in_the_same_second_is_accepted`, `The_latest_revocation_wins_whatever_order_they_arrive_in`,
`A_revocation_is_forgotten_once_every_token_it_could_refuse_has_expired`, `A_validated_token_is_asked_by_its_sub_and_iat`,
`Every_service_refuses_a_revoked_token_when_it_validates_it`, `The_consumer_records_what_Identity_published`, and the
publishers `A_lock_revokes`, `A_ban_revokes`, `Revoking_a_role_revokes`, `Changing_the_password_revokes`,
`Resetting_the_password_revokes`, `A_reused_refresh_token_revokes`, `Granting_a_role_revokes_nothing`. At the merge the
whole server suite was 635/635 in 9 projects.

---

## Scenario 2 - A ban, across services (US1, SC-001)

```bash
probe() {
  # identity, cart, order, activity: the customer's own; catalog and inventory: Seller / Admin only (403 before)
  for url in auth/me cart orders notifications products/mine reservations/00000000-0000-0000-0000-000000000000; do
    curl -s -o /dev/null -w '%{http_code} ' "http://localhost:5000/api/$url" -H "Authorization: Bearer $CUSTOMER"
  done; echo
}
probe                                                   # before
curl -s -o /dev/null -w 'ban: %{http_code}\n' -X POST "http://localhost:5000/api/users/$USER_ID/ban" \
  -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"reason":"Fraud"}'
sleep 2; probe                                          # after
```

**Expected**: every protected call answers 401 after the ban. The pull request's run (identity, cart, order,
activity, catalog, inventory; the last two were endpoints the customer was not allowed, hence 403 before):

```text
before (identity cart order activity catalog inventory): 200 200 200 200 403 403
ban: 200
after  (identity cart order activity catalog inventory): 401 401 401 401 401 401   in 1758 ms
```

The exact endpoints probed in that run are not recorded; the ones above give the same pattern, and any
`[Authorize]` endpoint of each service answers 401 once the token is revoked.

---

## Scenario 3 - A password change keeps this browser (US1, SC-002)

```bash
curl -s -o /dev/null -w 'change: %{http_code}\n' -b c.jar -X PUT http://localhost:5000/api/auth/me/password \
  -H "Authorization: Bearer $CUSTOMER" -H 'Content-Type: application/json' \
  -d '{"currentPassword":"Passw0rd!23","newPassword":"NewPassw0rd!45"}'
sleep 2
curl -s -o /dev/null -w 'old access token on cart: %{http_code}\n' http://localhost:5000/api/cart -H "Authorization: Bearer $CUSTOMER"
NEW=$(curl -fsS -b c.jar -X POST http://localhost:5000/api/auth/refresh | jq -r .token)
curl -s -o /dev/null -w 'after refresh, new token on cart: %{http_code}\n' http://localhost:5000/api/cart -H "Authorization: Bearer $NEW"
```

**Expected** - as recorded:

```text
change: 204
old access token on cart: 401
after refresh, new token on cart: 200
```

---

## Scenario 4 - One temporary queue per instance (US2)

```bash
docker exec e-commerce-rabbitmq rabbitmqctl list_queues name durable arguments consumers | grep access-revoked
```

**Expected**: one `<svc>-access-revoked-<id>` per running instance of identity, catalog, cart, order, inventory,
payment and activity, each `durable=false` with `x-expires` 60000 and one consumer - as recorded:

```text
cart-access-revoked-…  inventory-access-revoked-…  catalog-…  order-…  identity-…  activity-…  payment-…
(durable=false, x-expires=60000)
```

---

## Scenario 5 - Bruno

```bash
cd bruno
npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
```

**Expected**: 205/205 requests and 336/336 tests, including `admin-insights/the revoked moderator's token stops at
once` (401).

---

## Mutations (Principle V)

| Mutation | Caught by |
| :-- | :-- |
| compare the exact instant, not the whole second | `A_token_issued_in_the_same_second_is_accepted` |
| a late, older message overwrites a newer revocation | `The_latest_revocation_wins…` |
| revocations never forgotten | `A_revocation_is_forgotten…` |
| the hook never fails a token | `Every_service_refuses_a_revoked_token…` |
| a ban publishes nothing | `A_ban_revokes` |
| a password change publishes nothing | `Changing_the_password_revokes` |
