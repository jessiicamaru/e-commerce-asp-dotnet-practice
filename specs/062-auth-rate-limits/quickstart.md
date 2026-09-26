# Quickstart: Validating the limits

> Written on 2026-09-27, after the feature merged (#145), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Feature**: [spec.md](./spec.md) | **Contract**: [http-api.md](./contracts/http-api.md)

Scenarios 1 to 5 were run for the pull request; its recorded results are quoted.

---

## Prerequisites

```bash
cd server
docker compose -f docker-compose.yml -f docker-compose.app.yml up -d --build   # gateway trusts the storefront at 172.30.10.10
```

The allowances are per minute; wait a minute between scenarios that use the same one, or restart the gateway to
forgive them.

---

## Scenario 1 - The gateway through its real pipeline (US1)

```bash
cd server
dotnet test tests/Ecommerce.ApiGateway.Tests
```

**Expected**: 12/12 - no database needed. Among them `Asking_for_reset_links_too_fast_is_429_with_how_long_to_wait`,
`Using_up_one_limit_leaves_the_others`, `Nothing_else_is_limited`,
`A_forwarded_address_from_an_untrusted_peer_is_ignored`, `A_trusted_proxy_forwards_each_client_address`,
`Only_the_hop_the_trusted_proxy_added_counts` and the `A_setting_that_would_switch_a_limit_off_refuses_to_start`
theory.

## Scenario 2 - Identity's pause and interval (US2, US3)

```bash
cd server
DB_PASSWORD=<your password> dotnet test tests/Ecommerce.Identity.Tests \
  --filter "FullyQualifiedName~SignInThrottleTests|FullyQualifiedName~ForbiddenProblemTests.Too_many_requests_says_how_long_to_wait"
```

**Expected**: all pass - 14 in `SignInThrottleTests`, including
`Five_wrong_passwords_pause_the_email_even_for_the_right_one`, `An_unknown_email_is_answered_exactly_like_a_real_one`,
`Simultaneous_wrong_passwords_are_all_counted_and_pause_once` and `Asking_for_a_link_many_times_at_once_sends_one_email`.
At the merge the Identity suite was 116/116.

## Scenario 3 - The storefront's words (US4)

```bash
cd client
npm test -- src/utils/shared/too-many src/pages/sign-in src/pages/sign-up src/pages/forgot-password src/pages/reset-password
```

**Expected**: pass ("says how many minutes to wait, rounded up", "never says zero minutes", "reads Retry-After when
the body does not say" ...). At the merge: 313/313, `tsc -b` and oxlint clean.

---

## Scenario 4 - End to end on the compose stack (SC-001, SC-002, SC-004)

```bash
EMAIL=<a registered address>
for i in 1 2 3 4 5; do
  curl -s -o /dev/null -w '%{http_code} ' -X POST http://localhost:5000/api/auth/login \
    -H 'Content-Type: application/json' -d '{"email":"'"$EMAIL"'","password":"wrong"}'
done; echo
curl -si -X POST http://localhost:5000/api/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"'"$EMAIL"'","password":"<the right password>"}' | grep -iE '^HTTP|^retry-after|retryAfter'

# the email allowance, straight at the gateway, forging a new X-Forwarded-For each time
for i in 1 2 3 4 5 6; do
  curl -s -o /dev/null -w '%{http_code} ' -X POST http://localhost:5000/api/auth/forgot-password \
    -H "X-Forwarded-For: 10.0.0.$i" -H 'Content-Type: application/json' -d '{"email":"nobody@example.test"}'
done; echo
curl -s -o /dev/null -w 'products: %{http_code}\n' http://localhost:5000/api/products
```

**Expected** - the pull request's run printed:

```text
5 wrong: 401 401 401 401 401
right password -> HTTP/1.1 429, Retry-After: 300, {"detail":"Too many wrong passwords for this email. Try again later.", "retryAfter":300}
unknown email, 6 wrong: 401 401 401 401 401 429
through the storefront, 6 forgot (forged XFF each): 202 202 202 202 202 429
straight at the gateway, 6 forgot (forged XFF each): 202 202 202 202 202 429
products: 200
```

"Through the storefront" is the same loop against `http://localhost:8088/api/auth/forgot-password`.

```sql
-- Identity, localhost:5435, ecommerce_identity_db
SELECT "EmailKey", "Failures", "WindowStartedAt", "BlockedUntil" FROM sign_in_throttles;
```

## Scenario 5 - Bruno and the storefront image (SC-005)

```bash
cd bruno && npx @usebruno/cli run --env local --env-var "adminEmail=$ADMIN_EMAIL" --env-var "adminPassword=$ADMIN_PASSWORD"
../.github/scripts/verify-storefront-image.sh
```

**Expected**: the collection passes (316/316 tests in the pull request's run, with
`security-checks/asking for reset links too fast is 429` last in its folder), and the storefront image check still
passes.

---

## Mutations (Principle V)

Each was reverted and the file touched so MSBuild rebuilt:

| Mutation | Caught by |
| :-- | :-- |
| the pause is never asked | 4 throttle tests |
| unknown emails not counted | `An_unknown_email_is_answered_exactly_like_a_real_one`, purge test |
| success does not clear | `The_right_password_starts_the_count_again` |
| count not reset when a pause starts | `After_a_pause_a_full_run…`, `Simultaneous…pause_once` |
| a reset does not clear | `A_password_reset_starts_the_count_again` |
| no reset-email interval | `…twice_within_a_minute…`, `…many_times_at_once…` |
| interval without the row lock | `Asking_for_a_link_many_times_at_once_sends_one_email` |
| empty trust list reads the header | `A_forwarded_address_from_an_untrusted_peer_is_ignored` |
| forgot-password route without its policy | 6 gateway tests |
| `UseForwardedHeaders` removed | `A_trusted_proxy_forwards_each_client_address` |
