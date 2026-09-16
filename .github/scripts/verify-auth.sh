#!/usr/bin/env bash
#
# Verifies the authentication and authorization boundary against running services.
#
#   ADMIN_EMAIL=... ADMIN_PASSWORD=... .github/scripts/verify-auth.sh
#
# Requires the Identity and Catalog services to be up already. Used by CI, and
# runnable locally against `./start-dev.ps1` with the values from server/.env.
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

echo
echo "All authentication and authorization checks passed."
