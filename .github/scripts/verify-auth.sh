#!/usr/bin/env bash
#
# Verifies the authentication and authorization boundary against running services.
#
#   ADMIN_EMAIL=... ADMIN_PASSWORD=... ADMIN_TOTP_SECRET=... .github/scripts/verify-auth.sh
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
CART_URL="${CART_URL:-http://localhost:5062}"

: "${ADMIN_EMAIL:?ADMIN_EMAIL is required}"
: "${ADMIN_PASSWORD:?ADMIN_PASSWORD is required}"
: "${ADMIN_TOTP_SECRET:?ADMIN_TOTP_SECRET is required - staff sign in with a code (specs/110); see server/.env.example}"

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

# The current six-digit code for a base32 TOTP secret (RFC 6238; specs/110). OFFSET moves it by whole windows.
totp() {
  SECRET="$1" OFFSET="${2:-0}" "$PYTHON" -c '
import base64, hmac, hashlib, os, struct, time
secret = os.environ["SECRET"].replace(" ", "").upper()
key = base64.b32decode(secret + "=" * (-len(secret) % 8))
step = int(time.time()) // 30 + int(os.environ["OFFSET"])
h = hmac.new(key, struct.pack(">Q", step), hashlib.sha1).digest()
o = h[-1] & 15
print(str((struct.unpack(">I", h[o:o + 4])[0] & 0x7FFFFFFF) % 10**6).zfill(6))
'
}

# Signs the administrator in, both steps (specs/110), and prints the access token. A code works once: when this
# window's was already used - another script a moment ago - it waits for the next window and asks again.
admin_sign_in() {
  local first challenge answer token attempt
  for attempt in 1 2; do
    first=$(curl -fsS -X POST "$IDENTITY_URL/api/auth/login" -H 'Content-Type: application/json' \
      -d "$(json_object email "$ADMIN_EMAIL" password "$ADMIN_PASSWORD")") || return 1
    challenge=$(printf '%s' "$first" | json_field challenge)
    [ -n "$challenge" ] || return 1
    answer=$(curl -sS -X POST "$IDENTITY_URL/api/auth/login/two-factor" -H 'Content-Type: application/json' \
      -d "$(json_object challenge "$challenge" code "$(totp "$ADMIN_TOTP_SECRET")")")
    token=$(printf '%s' "$answer" | json_field token 2>/dev/null || true)
    if [ -n "$token" ]; then
      printf '%s' "$token"
      return 0
    fi
    sleep $(( 31 - $(date +%s) % 30 ))
  done
  return 1
}

echo "Identity: $IDENTITY_URL"
echo "Catalog:  $CATALOG_URL"
echo

# 1. The bootstrap administrator was seeded and signs in - in two steps (specs/110): the right password alone issues
#    no token, only a challenge, and the code from the authenticator exchanges it for the session.
first_step=$(curl -fsS -X POST "$IDENTITY_URL/api/auth/login" -H 'Content-Type: application/json' \
  -d "$(json_object email "$ADMIN_EMAIL" password "$ADMIN_PASSWORD")")
[ -z "$(printf '%s' "$first_step" | json_field token)" ] || fail "The right password alone issued an access token; staff need a code too."
[ "$(printf '%s' "$first_step" | json_field twoFactor)" = "Required" ] || fail "The administrator's first step did not ask for a code."
pass "the right password alone issues no token, only a challenge"

admin_token=$(admin_sign_in) || fail "The seeded administrator could not sign in with a code. Check ADMIN_TOTP_SECRET against what Identity seeded."
pass "seeded administrator signs in with a code"

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

# 4b. A staff role without a second factor opens nothing - in another service, too (specs/110 SC-001). A new account
#     is made a moderator; signed in with its password only, its session carries no Moderator role, so Catalog's
#     review queue refuses it.
mod_email="moderator-$(date +%s)-$RANDOM@ci.local"
mod_id=$(curl -fsS -X POST "$IDENTITY_URL/api/auth/register" -H 'Content-Type: application/json' \
  -d "$(json_object email "$mod_email" password 'Passw0rd!23' firstName CI lastName Moderator)" | json_field id)
granted=$(status -X PUT "$IDENTITY_URL/api/users/$mod_id/roles/Moderator" -H "Authorization: Bearer $admin_token")
[ "$granted" = "200" ] || fail "Granting Moderator answered $granted."
mod_session=$(curl -fsS -X POST "$IDENTITY_URL/api/auth/login" -H 'Content-Type: application/json' \
  -d "$(json_object email "$mod_email" password 'Passw0rd!23')")
[ "$(printf '%s' "$mod_session" | json_field twoFactor)" = "SetupRequired" ] || fail "A moderator without two-factor sign-in was not told to set it up."
mod_token=$(printf '%s' "$mod_session" | json_field token)
[ "$(jwt_claim "$mod_token" role)" != "Moderator" ] || fail "A moderator's session without a second factor carried role=Moderator."
queue=$(status "$CATALOG_URL/api/products/review?status=Pending" -H "Authorization: Bearer $mod_token")
[ "$queue" = "403" ] || fail "Catalog's review queue answered $queue to a moderator without a second factor; it must be 403."
pass "a moderator without a second factor is refused by another service (403)"

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

  # The product has to be real now.
  #
  # This used to order productId 11111111-1111-1111-1111-111111111111 - an id that
  # has never existed in any catalogue - with a name and a price supplied in the
  # body, and it worked. Order believed whatever it was told, which is the defect
  # closed by feature 009 (issue #18). It now asks Catalog, so a fictional product
  # is refused with 404 and this script would fail at the next line.
  #
  # Creating one here keeps this script about AUTHORIZATION rather than about
  # pricing - what is being asserted below is still only who may read whose orders.
  ORDER_RUN_ID="$(date +%s)$$"

  auth_category_id=$(
    curl -fsS -X POST "$CATALOG_URL/api/categories"       -H 'Content-Type: application/json' -H "Authorization: Bearer $admin_token"       -d "$(json_object name "Auth CI $ORDER_RUN_ID" slug "auth-ci-$ORDER_RUN_ID")" | json_field id
  )
  [ -n "$auth_category_id" ] || fail "Could not create a category for the order checks."

  auth_product_id=$(
    curl -fsS -X POST "$CATALOG_URL/api/products"       -H 'Content-Type: application/json' -H "Authorization: Bearer $admin_token"       -d "$("$PYTHON" -c '
import json, sys
print(json.dumps({
    "name": "CI Widget " + sys.argv[1],
    "description": None,
    # Whole dong: the shop prices in VND by default and dong has no decimal
    # places, so 9.99 is not an amount and Catalog refuses it (specs/022).
    "price": 990000,
    "sku": "AUTHCI" + sys.argv[1],
    "categoryId": sys.argv[2],
}))
' "$ORDER_RUN_ID" "$auth_category_id")" | json_field id
  )
  [ -n "$auth_product_id" ] || fail "Could not create a product for the order checks."

  # This run's product and category go when the script ends, whatever the outcome (specs/073,
  # #118) - a delete that fails is said, never fatal, and the exit status is kept.
  auth_cleanup() {
    echo "      cleanup: product $auth_product_id -> HTTP $(curl -s -o /dev/null -w '%{http_code}' -X DELETE -H "Authorization: Bearer $admin_token" "$CATALOG_URL/api/products/$auth_product_id" || true)"
    echo "      cleanup: category $auth_category_id -> HTTP $(curl -s -o /dev/null -w '%{http_code}' -X DELETE -H "Authorization: Bearer $admin_token" "$CATALOG_URL/api/categories/$auth_category_id" || true)"
  }
  trap auth_cleanup EXIT

  # An order is placed THROUGH THE CART now (feature 010): put the product in the
  # customer's cart, then check out with no body. What is bought comes from the
  # cart, who is buying from the token, and what it costs from Catalog.
  cart_line="$("$PYTHON" -c '
import json, sys
print(json.dumps({"productId": sys.argv[1], "quantity": 1}))
' "$auth_product_id")"

  curl -fsS -X POST "$CART_URL/api/cart/items"     -H 'Content-Type: application/json'     -H "Authorization: Bearer $customer_token"     -d "$cart_line" > /dev/null     || fail "The customer could not add to their cart."

  # Somewhere to send it (feature 011): checkout now names an address and a delivery
  # option. The address is saved in Identity and read by Order with this token.
  address_id=$(
    curl -fsS -X POST "$IDENTITY_URL/api/addresses"       -H 'Content-Type: application/json' -H "Authorization: Bearer $customer_token"       -d "$(json_object recipientName "CI Customer" line1 "1 Test Street" city "Ha Noi" postalCode "100000" country "VN")"       | json_field id
  )
  [ -n "$address_id" ] || fail "The customer could not save a delivery address."
  pass "customer saves a delivery address ($address_id)"

  order_id=$(
    curl -fsS -X POST "$ORDER_URL/api/orders"       -H 'Content-Type: application/json' -H "Authorization: Bearer $customer_token"       -d "$(json_object addressId "$address_id" shippingOption standard)" | json_field orderId
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

  # Addresses, with two real signed tokens (feature 011, SC-003). Another customer's
  # address must answer exactly like an address that does not exist - for reading,
  # changing, deleting and checking out with it.
  random_id="$("$PYTHON" -c 'import uuid; print(uuid.uuid4())')"
  for target in "$address_id" "$random_id"; do
    label=$([ "$target" = "$address_id" ] && echo "another customer's" || echo "a nonexistent")
    got_get=$(status "$IDENTITY_URL/api/addresses/$target" -H "Authorization: Bearer $other_token")
    got_put=$(status -X PUT "$IDENTITY_URL/api/addresses/$target" -H "Authorization: Bearer $other_token"       -H 'Content-Type: application/json'       -d "$(json_object recipientName Mallory line1 "1 Evil St" city Nowhere postalCode 00000 country GB)")
    got_del=$(status -X DELETE "$IDENTITY_URL/api/addresses/$target" -H "Authorization: Bearer $other_token")
    [ "$got_get $got_put $got_del" = "404 404 404" ] || fail       "For $label address the other shopper got GET=$got_get PUT=$got_put DELETE=$got_del; all must be 404."
    pass "$label address is 404 to the other shopper, for read, change and delete"
  done

  # The other shopper checks out naming the first shopper's address. They have a cart
  # (so the refusal is about the address), and it must be the same 404 a random id gets.
  curl -fsS -X POST "$CART_URL/api/cart/items" -H 'Content-Type: application/json'     -H "Authorization: Bearer $other_token" -d "$cart_line" > /dev/null     || fail "The other shopper could not add to their cart."
  stolen=$(status -X POST "$ORDER_URL/api/orders" -H 'Content-Type: application/json'     -H "Authorization: Bearer $other_token" -d "$(json_object addressId "$address_id" shippingOption standard)")
  [ "$stolen" = "404" ] || fail     "Checking out with another customer's address answered $stolen; it must be refused with 404, like a missing address."
  pass "checking out with another customer's address is refused (404)"

  still_mine=$(curl -fsS "$IDENTITY_URL/api/addresses/$address_id" -H "Authorization: Bearer $customer_token" | json_field recipientName)
  [ "$still_mine" = "CI Customer" ] || fail "The owner's address changed to '$still_mine' after the other shopper's attempts."
  pass "the owner's address is untouched"
fi

echo
if [ "${SKIPPED_ORDER_CHECKS:-0}" = "1" ]; then
  echo "Authentication and authorization checks passed, EXCEPT the order ownership"
  echo "checks, which were skipped because the Order service was not running."
else
  echo "All authentication and authorization checks passed."
fi
