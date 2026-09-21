#!/usr/bin/env bash
#
# Verifies the checkout saga end to end, against running services.
#
#   ADMIN_EMAIL=... ADMIN_PASSWORD=... .github/scripts/verify-saga.sh
#
# Places a real order over HTTP with a real signed customer token, follows it to
# a terminal state, and asserts that the stock actually moved. Every service's
# unit tests are confined to that service; this is the only check that can see
# the gaps BETWEEN them, which is where this system's characteristic failures
# live. Inventory and Order once each declared a consumer class named
# OrderCompletedConsumer, so both bound to one queue and competed for the event:
# the order settled, the stock stayed held, and all fifteen Order tests were
# green throughout.
#
# Payment decides its outcome ONCE at startup, so one running Payment produces
# one of the two scenarios. This script runs the one that matches what Payment
# reports; exercising both means invoking it twice with Payment restarted in
# between. See specs/007-saga-e2e-verification/contracts/verify-saga.md.
#
# Needs only bash, curl and Python - no jq.
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
[ -n "$PYTHON" ] || { echo "python3 or python is required" >&2; exit 2; }

IDENTITY_URL="${IDENTITY_URL:-http://localhost:5056}"
CATALOG_URL="${CATALOG_URL:-http://localhost:5057}"
ORDER_URL="${ORDER_URL:-http://localhost:5059}"
INVENTORY_URL="${INVENTORY_URL:-http://localhost:5060}"
PAYMENT_URL="${PAYMENT_URL:-http://localhost:5061}"
CART_URL="${CART_URL:-http://localhost:5062}"

SAGA_TIMEOUT_SECONDS="${SAGA_TIMEOUT_SECONDS:-60}"
SAGA_E2E_REQUIRE_ALL="${SAGA_E2E_REQUIRE_ALL:-}"
SAGA_E2E_SCENARIO="${SAGA_E2E_SCENARIO:-}"

: "${ADMIN_EMAIL:?ADMIN_EMAIL is required}"
: "${ADMIN_PASSWORD:?ADMIN_PASSWORD is required}"

# How many units this run orders. Small, and smaller than what it puts on the
# shelf, so the order can never fail for lack of stock - a failure here must
# mean the saga is wrong, not that the fixture was too tight.
ORDER_QUANTITY=3
STOCK_ON_HAND=50

fail() {
  # ::error:: is picked up by GitHub Actions and ignored elsewhere.
  echo "::error::$1" >&2
  exit 1
}

pass() { echo "  ok  $1"; }
note() { echo "      $1"; }

# A scenario that could not run. Honest locally, where three of six services up
# is a normal afternoon; fatal in CI, where the job itself started all six, so
# "not listening" is the failure rather than an excuse for one.
SKIP_REASON=""
skip() {
  SKIP_REASON="$1"
  echo "  --  SKIPPED: $1"
}

# Reads one field out of a JSON document on stdin.
json_field() {
  FIELD="$1" "$PYTHON" -c '
import json, os, sys
print(json.load(sys.stdin).get(os.environ["FIELD"], ""))
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

# curl already prints 000 on a refused connection, and it also exits non-zero,
# which would end the run under `set -e`. `|| true` keeps the 000 and drops the
# exit code - `|| echo 000` would append a SECOND 000 and report "HTTP 000000",
# which reads like a status code and is not one.
status() { curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$@" || true; }

post_json() {
  # post_json <url> <body> [bearer]
  if [ -n "${3:-}" ]; then
    curl -s --max-time 20 -X POST "$1" -H 'Content-Type: application/json' \
      -H "Authorization: Bearer $3" -d "$2"
  else
    curl -s --max-time 20 -X POST "$1" -H 'Content-Type: application/json' -d "$2"
  fi
}

put_json() {
  curl -s --max-time 20 -X PUT "$1" -H 'Content-Type: application/json' \
    -H "Authorization: Bearer $3" -d "$2"
}

get_json() {
  if [ -n "${2:-}" ]; then
    curl -s --max-time 20 "$1" -H "Authorization: Bearer $2"
  else
    curl -s --max-time 20 "$1"
  fi
}

# ---------------------------------------------------------------- discovery

echo "Identity:  $IDENTITY_URL"
echo "Catalog:   $CATALOG_URL"
echo "Order:     $ORDER_URL"
echo "Inventory: $INVENTORY_URL"

PAYMENT_HEALTH="$(get_json "$PAYMENT_URL/health" || true)"
PAYMENT_PROVIDER=""
PAYMENT_OUTCOME_REPORTED=""
if [ -n "$PAYMENT_HEALTH" ]; then
  PAYMENT_PROVIDER="$(printf '%s' "$PAYMENT_HEALTH" | json_field provider 2>/dev/null || true)"
  PAYMENT_OUTCOME_REPORTED="$(printf '%s' "$PAYMENT_HEALTH" | json_field configuredOutcome 2>/dev/null || true)"
fi

if [ -n "$PAYMENT_OUTCOME_REPORTED" ]; then
  echo "Payment:   $PAYMENT_URL  (provider=$PAYMENT_PROVIDER, outcome=$PAYMENT_OUTCOME_REPORTED)"
else
  echo "Payment:   $PAYMENT_URL  (not answering)"
fi
echo

# Which scenario this invocation can run.
#
# The two vocabularies do not match and it is easy to lose an hour here:
# PAYMENT_OUTCOME is set to "Approve"/"Reject", while /health reports the
# resulting PaymentStatus, "Approved"/"Rejected". Input is the verb, output the
# past participle. Matching the health field against "Approve" finds nothing and
# silently takes the wrong branch.
SCENARIO=""
case "$PAYMENT_OUTCOME_REPORTED" in
  Approved) SCENARIO="approve" ;;
  Rejected) SCENARIO="reject" ;;
esac

# The override exists for the negative control: forcing "approve" against a
# refusing Payment must make this script fail, which is what proves the
# assertions are doing anything at all.
if [ -n "$SAGA_E2E_SCENARIO" ]; then
  case "$SAGA_E2E_SCENARIO" in
    approve|reject) SCENARIO="$SAGA_E2E_SCENARIO" ;;
    *) fail "SAGA_E2E_SCENARIO is '$SAGA_E2E_SCENARIO', which is neither 'approve' nor 'reject'." ;;
  esac
  note "scenario forced to '$SCENARIO' (Payment reports '${PAYMENT_OUTCOME_REPORTED:-nothing}')"
fi

# ---------------------------------------------------------------- reachability

unreachable=""
for pair in "Identity:$IDENTITY_URL" "Catalog:$CATALOG_URL" "Order:$ORDER_URL" \
            "Inventory:$INVENTORY_URL" "Payment:$PAYMENT_URL" "Cart:$CART_URL"; do
  name="${pair%%:*}"
  url="${pair#*:}"
  code="$(status "$url/health")"
  if [ "$code" != "200" ]; then
    # 000 means nothing is listening; anything else means it answered and is
    # reporting unhealthy. Different problems, so say which.
    if [ "$code" = "000" ]; then
      unreachable="$unreachable $name(not listening)"
    else
      unreachable="$unreachable $name(unhealthy, HTTP $code)"
    fi
  fi
done

report_and_exit() {
  # $1 = outcome word for the scenario
  echo
  if [ -n "$SKIP_REASON" ]; then
    echo "0 of 1 scenario exercised: ${SCENARIO:-unknown}=SKIPPED"
    if [ "$SAGA_E2E_REQUIRE_ALL" = "1" ]; then
      fail "A scenario was skipped while SAGA_E2E_REQUIRE_ALL=1: $SKIP_REASON"
    fi
    exit 0
  fi
  echo "1 of 1 scenario exercised: $SCENARIO=$1"
  exit 0
}

if [ -n "$unreachable" ]; then
  skip "not every service is available:$unreachable"
  report_and_exit skipped
fi

if [ -z "$SCENARIO" ]; then
  skip "Payment did not report a configuredOutcome, so neither scenario can be chosen"
  report_and_exit skipped
fi

# ---------------------------------------------------------------- setup

RUN_ID="$(date +%s)$$"

ADMIN_TOKEN="$(post_json "$IDENTITY_URL/api/auth/login" \
  "$(json_object email "$ADMIN_EMAIL" password "$ADMIN_PASSWORD")" | json_field token)"
[ -n "$ADMIN_TOKEN" ] || fail "The administrator could not sign in. Check ADMIN_EMAIL and ADMIN_PASSWORD against what Identity seeded."
pass "administrator signed in"

CATEGORY_ID="$(post_json "$CATALOG_URL/api/categories" \
  "$(json_object name "E2E $RUN_ID" description "created by verify-saga.sh" slug "e2e-$RUN_ID")" \
  "$ADMIN_TOKEN" | json_field id)"
[ -n "$CATEGORY_ID" ] || fail "Could not create a category. The administrator signed in, so this is Catalog rejecting the request rather than an authorization problem."

# The catalogue price, named once so the order assertion can compare against it
# rather than against a number repeated in two places.
PRODUCT_PRICE="9.99"

PRODUCT_BODY="$("$PYTHON" -c '
import json, sys
print(json.dumps({
    "name": "E2E Widget " + sys.argv[1],
    "description": "created by verify-saga.sh",
    "price": float(sys.argv[3]),
    "sku": "E2E" + sys.argv[1],
    "categoryId": sys.argv[2],
}))
' "$RUN_ID" "$CATEGORY_ID" "$PRODUCT_PRICE")"

PRODUCT_ID="$(post_json "$CATALOG_URL/api/products" "$PRODUCT_BODY" "$ADMIN_TOKEN" | json_field id)"
[ -n "$PRODUCT_ID" ] || fail "Could not create a product in category $CATEGORY_ID."

# A fresh product per run is what makes every later reading trustworthy: nothing
# else is ordering it, so a non-zero 'reserved' in the before-reading could only
# be this script's own doing.
pass "product created: $PRODUCT_ID"

# The stock row does NOT exist yet. Catalog publishes ProductCreatedEvent and
# Inventory's ProductCreatedConsumer creates the row in response, so there is a
# real asynchronous gap here - inside the SETUP. Treating it as synchronous
# produces a failure that reads exactly like a saga failure and is not one.
registered_after=""
for i in $(seq 1 30); do
  if [ "$(status "$INVENTORY_URL/api/stock/$PRODUCT_ID")" = "200" ]; then
    registered_after="$i"
    break
  fi
  sleep 1
done
[ -n "$registered_after" ] || fail "Inventory never registered a stock row for product $PRODUCT_ID (waited 30s). Catalog created the product, so ProductCreatedEvent was either not published or not consumed - check that Catalog's outbox is delivering and that Inventory is consuming."
pass "stock row registered after ${registered_after}s"

put_json "$INVENTORY_URL/api/stock/$PRODUCT_ID" \
  "$("$PYTHON" -c 'import json,sys;print(json.dumps({"quantityOnHand": int(sys.argv[1])}))' "$STOCK_ON_HAND")" \
  "$ADMIN_TOKEN" > /dev/null
pass "stock set to $STOCK_ON_HAND on hand"

CUSTOMER_EMAIL="e2e-$RUN_ID@example.test"
CUSTOMER_TOKEN="$(post_json "$IDENTITY_URL/api/auth/register" \
  "$(json_object email "$CUSTOMER_EMAIL" password "E2e-Passw0rd!$RUN_ID" firstName "E2E" lastName "Customer")" \
  | json_field token)"
[ -n "$CUSTOMER_TOKEN" ] || fail "Could not register a customer. Self-registration always grants Customer, so this is not a role problem."
pass "customer registered and signed in"

# ---------------------------------------------------------------- readings

read_stock() {
  get_json "$INVENTORY_URL/api/stock/$PRODUCT_ID"
}

BEFORE="$(read_stock)"
BEFORE_ON_HAND="$(printf '%s' "$BEFORE" | json_field quantityOnHand)"
BEFORE_RESERVED="$(printf '%s' "$BEFORE" | json_field quantityReserved)"
BEFORE_AVAILABLE="$(printf '%s' "$BEFORE" | json_field quantityAvailable)"
pass "stock before: on-hand=$BEFORE_ON_HAND reserved=$BEFORE_RESERVED available=$BEFORE_AVAILABLE"

# The order is placed THROUGH THE CART, the way a customer does it: put things in
# the cart, then check out. Checkout takes no body - what is bought comes from the
# cart, who is buying from the token, and what it costs from Catalog.
#
# Both requests still carry a FABRICATED price and a FABRICATED name, on purpose,
# on every run, as raw JSON with extra properties. Until 2026-09-21 the server
# believed a price in the request (issue #18): a product listed at 40,000,000 was
# bought for 1. The cart is a new place a price could be smuggled in - through the
# add-to-cart body, or through a body sent to checkout that should now be ignored
# entirely - so both routes are tried, and the charge must still be the catalogue's.
FABRICATED_PRICE="0.01"
FABRICATED_NAME="FABRICATED - the server must ignore this"

HOSTILE_LINE="$("$PYTHON" -c '
import json, sys
print(json.dumps({
    "productId": sys.argv[1],
    "quantity": int(sys.argv[2]),
    "unitPrice": float(sys.argv[3]),
    "productName": sys.argv[4],
}))
' "$PRODUCT_ID" "$ORDER_QUANTITY" "$FABRICATED_PRICE" "$FABRICATED_NAME")"

add_code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 -X POST "$CART_URL/api/cart/items" \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $CUSTOMER_TOKEN" -d "$HOSTILE_LINE" || true)"
[ "$add_code" = "204" ] || fail "Could not add to the cart (HTTP $add_code)."
pass "added $ORDER_QUANTITY to the cart (claiming $FABRICATED_PRICE each)"

CART_BEFORE_QTY="$(get_json "$CART_URL/api/cart" "$CUSTOMER_TOKEN" | "$PYTHON" -c '
import json, sys
lines = json.load(sys.stdin).get("lines", [])
print(next((l["quantity"] for l in lines if l["productId"] == sys.argv[1]), 0))
' "$PRODUCT_ID")"
[ "$CART_BEFORE_QTY" = "$ORDER_QUANTITY" ] \
  || fail "The cart holds $CART_BEFORE_QTY of the product after adding $ORDER_QUANTITY."
pass "cart holds $CART_BEFORE_QTY"

# A body sent to checkout is IGNORED - there is no field for it to bind to. Sent
# anyway, because a determined caller can always put whatever it likes on the wire.
HOSTILE_ORDER="$("$PYTHON" -c '
import json, sys
print(json.dumps({"items": [{"productId": sys.argv[1], "quantity": 99,
                             "unitPrice": float(sys.argv[2]), "productName": sys.argv[3]}]}))
' "$PRODUCT_ID" "$FABRICATED_PRICE" "$FABRICATED_NAME")"

ORDER_RESPONSE="$(post_json "$ORDER_URL/api/orders" "$HOSTILE_ORDER" "$CUSTOMER_TOKEN")"
ORDER_ID="$(printf '%s' "$ORDER_RESPONSE" | json_field orderId)"
[ -n "$ORDER_ID" ] || fail "Checkout was not accepted. Response: $ORDER_RESPONSE"
pass "order $ORDER_ID checked out from the cart (body claiming 99 at $FABRICATED_PRICE ignored)"

# The assertion the whole of issue #18 turns on: the shop decides what things cost.
ORDER_TOTAL="$(printf '%s' "$ORDER_RESPONSE" | json_field totalAmount)"
EXPECTED_TOTAL="$("$PYTHON" -c '
import sys
print(f"{float(sys.argv[1]) * int(sys.argv[2]):.2f}")
' "$PRODUCT_PRICE" "$ORDER_QUANTITY")"
ACTUAL_TOTAL="$("$PYTHON" -c 'import sys; print(f"{float(sys.argv[1]):.2f}")' "$ORDER_TOTAL")"

if [ "$ACTUAL_TOTAL" = "$EXPECTED_TOTAL" ]; then
  pass "charged the catalogue price, not the claimed one ($EXPECTED_TOTAL, claimed $FABRICATED_PRICE each)"
else
  fail "The customer set the price. Claimed $FABRICATED_PRICE each and the order totals $ACTUAL_TOTAL, where the catalogue price of $PRODUCT_PRICE x $ORDER_QUANTITY is $EXPECTED_TOTAL. The price on an order line must come from Catalog, which owns it - never from the request body. See issue #18 and specs/009-catalog-owns-price."
fi

ORDER_ITEM_NAME="$(printf '%s' "$ORDER_RESPONSE" | "$PYTHON" -c '
import json, sys
print(json.load(sys.stdin)["items"][0]["productName"])
')"

if [ "$ORDER_ITEM_NAME" = "$FABRICATED_NAME" ]; then
  fail "The customer named the product. The order line reads '"'"'$ORDER_ITEM_NAME'"'"', which is what the request claimed. The name belongs to Catalog, and is copied onto the line so the order still describes itself after the product is renamed."
fi
pass "recorded the catalogue name, not the claimed one ($ORDER_ITEM_NAME)"

# ---------------------------------------------------------------- settle

FINAL_STATUS=""
LAST_STATUS=""
settled_after=""
for i in $(seq 1 "$SAGA_TIMEOUT_SECONDS"); do
  LAST_STATUS="$(get_json "$ORDER_URL/api/orders/$ORDER_ID" "$CUSTOMER_TOKEN" | json_field status)"
  case "$LAST_STATUS" in
    Completed|Failed)
      FINAL_STATUS="$LAST_STATUS"
      settled_after="$i"
      break
      ;;
    Submitted|"")
      ;;
    *)
      # Pending, StockReserved, Paid and Cancelled are unreachable by design -
      # see specs/003-order-lifecycle/data-model.md. Observing one means the
      # system stopped matching the description this check is built on, which
      # is worth failing over rather than tolerating.
      fail "Order $ORDER_ID reported status '$LAST_STATUS', which is documented as unreachable. Either the lifecycle changed or something is writing a status the saga does not produce; this check's assumptions no longer hold."
      ;;
  esac
  sleep 1
done

if [ -z "$FINAL_STATUS" ]; then
  # A stall and a wrong outcome have different causes and different fixes.
  # Saying which one happened is the difference between a useful failure and a
  # red tick.
  fail "Order $ORDER_ID was still '${LAST_STATUS:-unknown}' after ${SAGA_TIMEOUT_SECONDS}s. It never settled, which is NOT the same as settling wrongly - look for a service that is down or a queue with the wrong number of consumers, rather than at the order's data. Start with the ORCHESTRATOR: it has no /health endpoint, so it is the one service the checks above could not confirm was running, and nothing moves without it. Then: docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages consumers"
fi

# Printing the elapsed time is what keeps the budget honest: if settlement
# normally takes three seconds and starts taking forty, that is visible long
# before the timeout is reached.
pass "order reached $FINAL_STATUS after ${settled_after}s"

AFTER="$(read_stock)"
AFTER_ON_HAND="$(printf '%s' "$AFTER" | json_field quantityOnHand)"
AFTER_RESERVED="$(printf '%s' "$AFTER" | json_field quantityReserved)"
AFTER_AVAILABLE="$(printf '%s' "$AFTER" | json_field quantityAvailable)"

# ---------------------------------------------------------------- assertions

# Assertions COLLECT rather than exit on the first one.
#
# Not a style choice. The negative control for this check runs the success
# scenario against a refusing Payment, and the only thing that proves the stock
# assertions are doing any work is watching them go red. Fail-fast would stop at
# the status assertion every time, and a stock assertion that has never been
# seen to fire is indistinguishable from one that cannot.
#
# It is also better when something really breaks: "the order failed AND the
# stock is wrong" and "the order failed but the stock is fine" point in
# different directions, and reporting only the first hides which.
FAILURES=0
assert_eq() {
  # assert_eq <actual> <expected> <ok message> <failure message>
  if [ "$1" = "$2" ]; then
    pass "$3"
  else
    echo "::error::$4" >&2
    FAILURES=$((FAILURES + 1))
  fi
}

if [ "$SCENARIO" = "approve" ]; then
  assert_eq "$FINAL_STATUS" "Completed" \
    "status is Completed" \
    "Order $ORDER_ID ended '$FINAL_STATUS', expected 'Completed'. The flow ran to a terminal state and reached the wrong one - this is not a stall."

  assert_eq "$AFTER_ON_HAND" "$((BEFORE_ON_HAND - ORDER_QUANTITY))" \
    "on-hand fell by exactly $ORDER_QUANTITY ($BEFORE_ON_HAND -> $AFTER_ON_HAND)" \
    "On-hand went $BEFORE_ON_HAND -> $AFTER_ON_HAND, expected $((BEFORE_ON_HAND - ORDER_QUANTITY)). The order completed but the units were never actually consumed, so the sale did not reduce the shelf."

  # The assertion that catches the bug this whole check exists for. Written as
  # "unchanged" rather than "zero" so a concurrent hold on the same product
  # cannot make it depend on nothing else happening.
  assert_eq "$AFTER_RESERVED" "$BEFORE_RESERVED" \
    "nothing left held (reserved $BEFORE_RESERVED -> $AFTER_RESERVED)" \
    "Stock is still held after the order completed: reserved went $BEFORE_RESERVED -> $AFTER_RESERVED, expected it unchanged. The order settled but Inventory never confirmed the reservation. Check whether two services declare a consumer class of the same name - they would bind to one queue and compete, so the event reaches one of them instead of both. Try: docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages consumers - a queue with 2 consumers that should have one subscriber per service is the symptom."

  assert_eq "$AFTER_AVAILABLE" "$((BEFORE_AVAILABLE - ORDER_QUANTITY))" \
    "available fell by exactly $ORDER_QUANTITY ($BEFORE_AVAILABLE -> $AFTER_AVAILABLE)" \
    "Available went $BEFORE_AVAILABLE -> $AFTER_AVAILABLE, expected $((BEFORE_AVAILABLE - ORDER_QUANTITY))."
else
  assert_eq "$FINAL_STATUS" "Failed" \
    "status is Failed" \
    "Order $ORDER_ID ended '$FINAL_STATUS', expected 'Failed' because Payment is configured to reject. The flow reached a terminal state and reached the wrong one."

  assert_eq "$AFTER_ON_HAND" "$BEFORE_ON_HAND" \
    "on-hand unchanged ($BEFORE_ON_HAND)" \
    "On-hand went $BEFORE_ON_HAND -> $AFTER_ON_HAND on an order that was never paid for. Stock was consumed by a failed order."

  # The quiet one. Stranded units come back when the expiry sweeper runs, so the
  # symptom is "stock reappeared several minutes later" - which nobody reports
  # as a bug and nobody can reproduce on demand.
  assert_eq "$AFTER_RESERVED" "$BEFORE_RESERVED" \
    "held units returned (reserved $BEFORE_RESERVED -> $AFTER_RESERVED)" \
    "Stock is stranded: reserved went $BEFORE_RESERVED -> $AFTER_RESERVED after a failed order, expected it unchanged. Compensation did not release the hold. The expiry sweeper will eventually return these units, which is exactly why this is easy to miss - the symptom is stock reappearing minutes later rather than an error."

  assert_eq "$AFTER_AVAILABLE" "$BEFORE_AVAILABLE" \
    "available back where it started ($BEFORE_AVAILABLE)" \
    "Available went $BEFORE_AVAILABLE -> $AFTER_AVAILABLE, expected it unchanged after a failed order."
fi

# ---------------------------------------------------------------- the cart after settlement
#
# Completed: the ordered lines leave the cart. Failed: the cart is left EXACTLY as
# it was, so a customer whose payment was declined can simply check out again -
# the saga's Failed is terminal, and removing the lines at submission would leave
# them with an empty cart and a dead order (specs/010-customer-cart, research D1).
#
# Polled, not read once: the cart learns of the outcome from an event and is a
# moment behind by design.
cart_qty() {
  get_json "$CART_URL/api/cart" "$CUSTOMER_TOKEN" | "$PYTHON" -c '
import json, sys
lines = json.load(sys.stdin).get("lines", [])
print(next((l["quantity"] for l in lines if l["productId"] == sys.argv[1]), 0))
' "$PRODUCT_ID"
}

if [ "$SCENARIO" = "approve" ]; then
  CART_AFTER=""
  for i in $(seq 1 20); do
    CART_AFTER="$(cart_qty)"
    [ "$CART_AFTER" = "0" ] && break
    sleep 1
  done
  assert_eq "$CART_AFTER" "0" \
    "the cart gave up what was ordered once the order completed" \
    "The order completed but the cart still holds $CART_AFTER of the product after 20s. Cart removes the ordered lines on OrderCompletedEvent, remembering the items from OrderSubmittedEvent; check both CartSvc queues exist with one consumer each. If Cart's consumers lost their CartSvc prefix they share a queue with Inventory's and Order's and compete for the event."
else
  # Give the failure event time to arrive before asserting nothing happened -
  # asserting "unchanged" instantly would pass even if the cart were wrongly
  # emptied a second later.
  sleep 3
  CART_AFTER="$(cart_qty)"
  assert_eq "$CART_AFTER" "$ORDER_QUANTITY" \
    "the cart is untouched after a declined payment (still $CART_AFTER)" \
    "The payment was declined and the order failed, but the cart went $ORDER_QUANTITY -> $CART_AFTER. A failed order must leave the cart alone, or a customer whose card was declined loses what they chose."
fi

if [ "$FAILURES" -gt 0 ]; then
  echo
  echo "$FAILURES of 5 assertions failed for scenario '$SCENARIO'."
  echo "1 of 1 scenario exercised: $SCENARIO=FAIL"
  exit 1
fi

report_and_exit pass
