#!/usr/bin/env bash
#
# Verifies the authentication and authorization boundary against running services.
#
#   ADMIN_EMAIL=... ADMIN_PASSWORD=... .github/scripts/verify-auth.sh
#
# Requires the Identity and Catalog services to be up already. The Order service
# is optional: if it is not listening, the order ownership checks report that they
# were skipped rather than passing silently. Used by CI, and runnable locally
# against `./start-dev.ps1` with the values from server/.env.
#
# Needs only bash, curl and Python — no jq.
set -euo pipefail

# Ubuntu runners ship python3; on Git Bash for Windows "python3" is often the
# Microsoft Store stub, which prints a notice and fails. Probe for one that runs.
PYTHON="${PYTHON:-}"
if [ -z "$PYTHON" ]; then
  for candidate in python3 python; do
    if command -v "$candidate" > /dev/null 2>&1 && "$candidate" -c '' > /dev/null 2>&1; then
      PYTHON="$candidate"
      break
    fi
  done
fi
[ -n "$PYTHON" ] || { echo "python3 or python is required" >&2; exit 1; }

IDENTITY_URL="${IDENTITY_URL:-http://localhost:5056}"
CATALOG_URL="${CATALOG_URL:-http://localhost:5057}"
ORDER_URL="${ORDER_URL:-http://localhost:5059}"

: "${ADMIN_EMAIL:?ADMIN_EMAIL is required}"
: "${ADMIN_PASSWORD:?ADMIN_PASSWORD is required}"

fail() {
  # ::error:: is picked up by GitHub Actions and ignored elsewhere.
  echo "::error::$1" >&2
  exit 1
}

pass() { echo "  ok  $1"; }

# Reads one field out of a JSON document on stdin.
json_field() {
  FIELD="$1" "$PYTHON" -c '
import json, os, sys
print(json.load(sys.stdin).get(os.environ["FIELD"], ""))
'
}

# Decodes one claim from a JWT payload. The signature is NOT verified; this only
# reports what the Identity service put in the token, never a trust decision.
jwt_claim() {
  ACCESS_TOKEN="$1" CLAIM="$2" "$PYTHON" -c '
import base64, json, os
payload = os.environ["ACCESS_TOKEN"].split(".")[1]
payload += "=" * (-len(payload) % 4)
print(json.loads(base64.urlsafe_b64decode(payload)).get(os.environ["CLAIM"], ""))
'
}

# Builds a JSON body safely, whatever characters the password contains.
json_object() {
  "$PYTHON" -c '
import json, sys
pairs = sys.argv[1:]
print(json.dumps(dict(zip(pairs[::2], pairs[1::2]))))
' "$@"
}

status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

echo "Identity: $IDENTITY_URL"
echo "Catalog:  $CATALOG_URL"
echo

# 1. The bootstrap administrator was seeded and can log in.
admin_token=$(
  curl -fsS -X POST "$IDENTITY_URL/api/auth/login" \
    -H 'Content-Type: application/json' \
    -d "$(json_object email "$ADMIN_EMAIL" password "$ADMIN_PASSWORD")" | json_field token
)
[ -n "$admin_token" ] || fail "The seeded administrator could not log in."
pass "seeded administrator logs in"

admin_role=$(jwt_claim "$admin_token" role)
[ "$admin_role" = "Admin" ] || fail "Expected the admin token to carry role=Admin, got '$admin_role'."
pass "admin token carries role=Admin"

# 2. Self-registration yields a plain Customer, never an Admin.
customer_email="customer-$(date +%s)-$RANDOM@ci.local"
customer_token=$(
  curl -fsS -X POST "$IDENTITY_URL/api/auth/register" \
    -H 'Content-Type: application/json' \
    -d "$(json_object email "$customer_email" password 'Passw0rd!23' firstName CI lastName User)" | json_field token
)
[ -n "$customer_token" ] || fail "Registration did not return a token."

customer_role=$(jwt_claim "$customer_token" role)
[ "$customer_role" = "Customer" ] || fail "A new account should get role=Customer, got '$customer_role'."
pass "self-registration grants Customer, not Admin"

# 3. The order owner is taken from the token, so 'sub' has to be there.
[ -n "$(jwt_claim "$customer_token" sub)" ] || fail "The token carries no 'sub' claim."
pass "token carries a 'sub' claim"

# 4. Admin-only endpoint. Admin is expected to get past authorization and fail
#    validation instead (the body is deliberately incomplete), so anything other
#    than 401/403 counts as allowed.
anon=$(status -X POST "$CATALOG_URL/api/categories" -H 'Content-Type: application/json' -d '{"name":"CI"}')
[ "$anon" = "401" ] || fail "Anonymous write should be 401, got $anon."
pass "anonymous write rejected with 401"

forbidden=$(status -X POST "$CATALOG_URL/api/categories" -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $customer_token" -d '{"name":"CI"}')
[ "$forbidden" = "403" ] || fail "Customer write should be 403, got $forbidden."
pass "customer write rejected with 403"

allowed=$(status -X POST "$CATALOG_URL/api/categories" -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $admin_token" -d '{"name":"CI"}')
{ [ "$allowed" != "401" ] && [ "$allowed" != "403" ]; } \
  || fail "Admin write was blocked with $allowed; the role claim is not being read correctly."
pass "admin write passes authorization (got $allowed)"

# 5. Public reads stay public.
public=$(status "$CATALOG_URL/api/products")
[ "$public" != "401" ] || fail "Anonymous product listing should not require a token."
pass "anonymous product listing still public"

# 6. An order belongs to the shopper who placed it, and to nobody else.
#
#    This is the only part of the order-read path that uses a REAL signed token.
#    The unit tests substitute ICurrentUser, so they prove the owner filter is
#    applied to whatever identity is handed in - not that the identity handed in
#    came from a valid token. That distinction is exactly what went wrong once
#    before in this repository, where a hand-minted token agreed with a
#    hand-written expectation while every real token was rejected.
# curl already prints 000 on a refused connection, but it also exits non-zero,
# and this script runs under `set -e`. The `|| true` keeps a missing Order
# service from ending the run before the skip is reported.
order_health=$(status --max-time 5 "$ORDER_URL/health" || true)

if [ "$order_health" = "000" ]; then
  SKIPPED_ORDER_CHECKS=1
  echo "  SKIPPED  order ownership checks - nothing listening on $ORDER_URL"
else
  SKIPPED_ORDER_CHECKS=0

  anon_orders=$(status "$ORDER_URL/api/orders")
  [ "$anon_orders" = "401" ] || fail "Anonymous order listing should be 401, got $anon_orders."
  pass "anonymous order listing rejected with 401"

  order_body='{"items":[{"productId":"11111111-1111-1111-1111-111111111111","productName":"CI Widget","quantity":1,"unitPrice":9.99}]}'

  order_id=$(
    curl -fsS -X POST "$ORDER_URL/api/orders" \
      -H 'Content-Type: application/json' \
      -H "Authorization: Bearer $customer_token" \
      -d "$order_body" | json_field orderId
  )
  [ -n "$order_id" ] || fail "The customer could not submit an order."
  pass "customer submits an order ($order_id)"

  # The owner reads it back. If the token's 'sub' were not reaching ICurrentUser,
  # this would be an empty list rather than an error - which is why the count is
  # asserted and not just the status code.
  own_count=$(
    curl -fsS "$ORDER_URL/api/orders" \
      -H "Authorization: Bearer $customer_token" | json_field totalCount
  )
  [ "${own_count:-0}" -ge 1 ] 2>/dev/null \
    || fail "The order owner sees '${own_count:-<nothing>}' of their own orders; expected at least 1."
  pass "order owner reads their own orders back (totalCount=$own_count)"

  own_detail=$(status "$ORDER_URL/api/orders/$order_id" -H "Authorization: Bearer $customer_token")
  [ "$own_detail" = "200" ] || fail "The owner should read their own order, got $own_detail."
  pass "order owner reads their own order by id"

  # A second, unrelated shopper.
  other_email="other-$(date +%s)-$RANDOM@ci.local"
  other_token=$(
    curl -fsS -X POST "$IDENTITY_URL/api/auth/register" \
      -H 'Content-Type: application/json' \
      -d "$(json_object email "$other_email" password 'Passw0rd!23' firstName CI lastName Other)" | json_field token
  )
  [ -n "$other_token" ] || fail "The second customer could not register."

  other_list=$(
    curl -fsS "$ORDER_URL/api/orders" \
      -H "Authorization: Bearer $other_token" | json_field totalCount
  )
  [ "$other_list" = "0" ] || fail "A different shopper sees $other_list orders; they should see none."
  pass "a different shopper sees none of that order"

  # 404 and not 403. A 403 would still 'reject the request' while confirming the
  # id is real, which is the disclosure this rule exists to prevent.
  other_detail=$(status "$ORDER_URL/api/orders/$order_id" -H "Authorization: Bearer $other_token")
  [ "$other_detail" = "404" ] || fail \
    "Another shopper's order should be 404 (indistinguishable from absent), got $other_detail."
  pass "another shopper's order is 404, not 403"
fi

echo
if [ "${SKIPPED_ORDER_CHECKS:-0}" = "1" ]; then
  echo "Authentication and authorization checks passed, EXCEPT the order ownership"
  echo "checks, which were skipped because the Order service was not running."
else
  echo "All authentication and authorization checks passed."
fi
