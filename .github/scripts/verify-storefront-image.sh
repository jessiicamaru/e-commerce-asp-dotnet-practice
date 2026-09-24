#!/usr/bin/env bash
#
# Asserts that a built storefront image actually SERVES the storefront (specs/051).
#
#   .github/scripts/verify-storefront-image.sh <image[:tag]>
#
# An image that builds but serves nothing is the failure this exists for: `docker build` succeeding says
# the bundle compiled, not that a browser gets an app, that a reload of /orders/123 is not a 404, or that
# /api reaches the gateway. So this starts the image against a STAND-IN gateway on its own network and
# asks it, over HTTP, the things a browser would.
#
# The stand-in answers every request with "stand-in <METHOD> <path> <body length>", so a forwarded call is
# recognisable and cannot be confused with the storefront answering it itself.
set -euo pipefail

IMAGE="${1:?usage: verify-storefront-image.sh <image[:tag]>}"
RUN="sf-verify-$$"
NET="$RUN-net"
STUB="$RUN-gateway"
APP="$RUN-app"
failures=0

cleanup() {
  docker rm -f "$APP" "$STUB" > /dev/null 2>&1 || true
  docker network rm "$NET" > /dev/null 2>&1 || true
}
trap cleanup EXIT

pass() { echo "  ok   $1"; }
fail() { echo "  FAIL $1"; failures=$((failures + 1)); }

docker image inspect "$IMAGE" > /dev/null 2>&1 || { echo "::error::no such image: $IMAGE" >&2; exit 1; }

# --- non-root, like the service images -------------------------------------------------------------
user="$(docker image inspect -f '{{.Config.User}}' "$IMAGE")"
if [ -n "$user" ] && [ "$user" != "root" ] && [ "$user" != "0" ]; then pass "runs as a non-root user ($user)"; else fail "runs as root (User='$user')"; fi

docker network create "$NET" > /dev/null

# The stand-in gateway: any method, any path, answers with what it received.
docker pull -q python:3.12-alpine > /dev/null
docker run -d --name "$STUB" --network "$NET" --network-alias gateway-stand-in python:3.12-alpine python -c '
import http.server
class H(http.server.BaseHTTPRequestHandler):
    def answer(self):
        n = int(self.headers.get("Content-Length") or 0)
        if n: self.rfile.read(n)
        body = f"stand-in {self.command} {self.path} {n}".encode()
        self.send_response(200)
        self.send_header("Content-Type", "text/plain")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)
    do_GET = do_POST = do_PUT = do_DELETE = answer
    def log_message(self, *a): pass
http.server.ThreadingHTTPServer(("", 8080), H).serve_forever()
' > /dev/null

docker run -d --name "$APP" --network "$NET" -p 127.0.0.1::8080 \
  -e GATEWAY_URL=http://gateway-stand-in:8080 "$IMAGE" > /dev/null

PORT="$(docker port "$APP" 8080/tcp | head -n1 | sed 's/.*://')"
BASE="http://127.0.0.1:$PORT"

# Both must be up before anything is asserted: the app, and the stand-in behind it.
for _ in $(seq 1 30); do
  curl -fsS -o /dev/null "$BASE/" 2> /dev/null && curl -fsS "$BASE/api/ready" 2> /dev/null | grep -q '^stand-in' && break
  sleep 1
done

status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }
# `|| true`: a MISSING header is a finding to report, and under `set -e -o pipefail` a grep that matches
# nothing would otherwise end the script silently - which the negative control for this very check did.
header() { curl -s -D - -o /dev/null "$1" | tr -d '\r' | { grep -i "^$2:" || true; } | head -n1 | cut -d' ' -f2-; }

# --- the app ---------------------------------------------------------------------------------------
index="$(curl -s "$BASE/")"
if printf '%s' "$index" | grep -q 'id="root"'; then pass "/ serves the app"; else fail "/ does not serve the app"; docker logs "$APP" | tail -20; fi

# --- a client-side route, opened directly (a reload, a shared link) --------------------------------
deep="$(curl -s -w '\n%{http_code}' "$BASE/orders/0199aa11-2233-7bbc-8ddd-eeeeffff0000")"
if [ "$(printf '%s' "$deep" | tail -n1)" = "200" ] && printf '%s' "$deep" | grep -q 'id="root"'; then
  pass "a deep link serves the app"
else
  fail "a deep link is not the app ($(printf '%s' "$deep" | tail -n1))"
fi

# --- a missing asset is a 404, not the app: a stale bundle must fail loudly, not load HTML as JS -----
code="$(status "$BASE/assets/does-not-exist.js")"
if [ "$code" = "404" ]; then pass "a missing asset is 404"; else fail "a missing asset answered $code"; fi

# --- /api reaches the gateway, path and query intact -----------------------------------------------
api="$(curl -s "$BASE/api/products?search=x-t5&page=2")"
if [ "$api" = "stand-in GET /api/products?search=x-t5&page=2 0" ]; then pass "/api is forwarded with its path and query"; else fail "/api answered: $api"; fi

# --- a photograph up to Catalog's 2 MB limit is not refused on the way ------------------------------
upload="$(head -c 2097152 /dev/zero | curl -s -X POST --data-binary @- -H 'Content-Type: application/octet-stream' "$BASE/api/products/x/image")"
if [ "$upload" = "stand-in POST /api/products/x/image 2097152" ]; then pass "a 2 MB upload reaches the gateway"; else fail "a 2 MB upload answered: ${upload:0:80}"; fi

# --- caching: index.html never, hashed assets forever ------------------------------------------------
cc="$(header "$BASE/" 'cache-control')"
if printf '%s' "$cc" | grep -q 'no-cache'; then pass "index.html is not cached ($cc)"; else fail "index.html Cache-Control: '$cc'"; fi
cc="$(header "$BASE/orders/1" 'cache-control')"
if printf '%s' "$cc" | grep -q 'no-cache'; then pass "a deep link is not cached"; else fail "deep link Cache-Control: '$cc'"; fi

asset="$(printf '%s' "$index" | grep -o '/assets/[^"]*\.js' | head -n1)"
if [ -n "$asset" ]; then
  cc="$(header "$BASE$asset" 'cache-control')"
  if [ "$(status "$BASE$asset")" = "200" ] && printf '%s' "$cc" | grep -q 'immutable'; then
    pass "a hashed asset is cached for good ($asset)"
  else
    fail "asset $asset: Cache-Control '$cc'"
  fi
else
  fail "index.html names no /assets/*.js"
fi

echo
if [ "$failures" -gt 0 ]; then
  echo "::error::$failures storefront image check(s) failed for $IMAGE"
  exit 1
fi
echo "The storefront image serves the storefront."
