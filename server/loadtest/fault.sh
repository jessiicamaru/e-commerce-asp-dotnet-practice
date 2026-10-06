#!/usr/bin/env bash
# Checkout through a fault (specs/147, #291):
#
#   server/loadtest/fault.sh payment|broker|orchestrator|inventory
#
# Starts resilience.js (steady checkouts), waits FAULT_AFTER seconds into the load, takes one thing away for FAULT_FOR
# seconds, brings it back, and lets the load finish. k6's teardown then waits for every order to settle and checks
# nothing was lost; afterwards this script checks no message faulted into an error queue. The timeline is kept beside
# k6's summary in server/loadtest/results/.
#
#   payment       Payment stopped, then started        - the saga holds the orders (its timeout is 600 s)
#   broker        RabbitMQ stopped, then started       - the outboxes hold the messages
#   orchestrator  the orchestrator restarted           - the saga's state is in its database
#   inventory     Inventory paused, then unpaused      - a hung service, not a dead one
set -uo pipefail

fault="${1:?usage: fault.sh payment|broker|orchestrator|inventory}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FAULT_AFTER="${FAULT_AFTER:-15}"
LOAD_FOR="${LOAD_FOR:-120s}"
RATE="${RATE:-3}"
case "$fault" in
  payment)      down() { docker stop ecommerce-payment >/dev/null; };        up() { docker start ecommerce-payment >/dev/null; };       container=ecommerce-payment ;;
  broker)       down() { docker stop e-commerce-rabbitmq >/dev/null; };      up() { docker start e-commerce-rabbitmq >/dev/null; };     container=e-commerce-rabbitmq ;;
  orchestrator) down() { docker stop ecommerce-orchestrator >/dev/null; };   up() { docker start ecommerce-orchestrator >/dev/null; };  container=ecommerce-orchestrator ;;
  inventory)    down() { docker pause ecommerce-inventory >/dev/null; };     up() { docker unpause ecommerce-inventory >/dev/null; };   container=ecommerce-inventory ;;
  *) echo "no fault '$fault' - payment, broker, orchestrator or inventory" >&2; exit 2 ;;
esac
# A restart is a fault of a few seconds; the others hold long enough to matter.
default_for=60
[ "$fault" = orchestrator ] && default_for=5
[ "$fault" = inventory ] && default_for=30
FAULT_FOR="${FAULT_FOR:-$default_for}"
# Enough customers that each comes round only after the fault has passed. While an order is pending its customer's
# cart still holds the line (Cart removes it on completion, specs/010), so a customer re-ordering during the outage
# would have the next line removed by the previous order's completion - an artefact of the harness, not of the fault.
PEOPLE="${PEOPLE:-$(( RATE * (FAULT_FOR + 20) ))}"

now() { date -u +%Y-%m-%dT%H:%M:%S.%3NZ; }
run_id="$(date -u +%Y-%m-%dT%H-%M-%S)"
log="$(mktemp)"
mkdir -p "$here/results"

echo "fault: $fault for ${FAULT_FOR}s, ${FAULT_AFTER}s into ${LOAD_FOR} of checkouts at ${RATE}/s by $PEOPLE customers (run $run_id)"
"$here/run.sh" resilience -e FAULT="$fault" -e RATE="$RATE" -e PEOPLE="$PEOPLE" -e LOAD_FOR="$LOAD_FOR" -e RUN_ID="$run_id" \
  -e SETTLE_TIMEOUT="${SETTLE_TIMEOUT:-300}" > "$log" 2>&1 &
k6=$!

# The load starts after k6's setup (signing in, listing a product, registering customers): count from there.
until grep -qE 'resilience +\[.*VUs' "$log" 2>/dev/null; do
  kill -0 "$k6" 2>/dev/null || { cat "$log"; echo "k6 ended before the load started" >&2; exit 1; }
  sleep 1
done
load_started_at="$(now)"
sleep "$FAULT_AFTER"

fault_at="$(now)"; down; echo "  $fault down at $fault_at"
sleep "$FAULT_FOR"
recovered_at="$(now)"; up; echo "  $fault back at $recovered_at"

# When it answers again, for the record: a container's health check, or running for a paused one.
for _ in $(seq 1 120); do
  state="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container" 2>/dev/null)"
  [ "$state" = healthy ] || [ "$state" = running ] && break
  sleep 1
done
healthy_at="$(now)"

# The broker's health check passing is not the broker accepting connections (specs/154): RabbitMQ's node answers ping
# seconds before it opens 5672, so "recovered" and "healthy" both came early. When it really opened the port, from its
# own log; and when each service came back, from the first connection it accepted from that service's address.
ready_at=""
if [ "$fault" = broker ]; then
  for _ in $(seq 1 180); do
    ready_at="$(docker logs --timestamps --since "$recovered_at" e-commerce-rabbitmq 2>&1 | grep -m1 'started TCP listener on' | cut -d' ' -f1)"
    [ -n "$ready_at" ] && break
    sleep 1
  done
fi

wait "$k6"; k6_exit=$?

reconnect="{}"
# Which reconnect schedule the services ran (specs/154): Messaging__ReconnectQuickly=false is MassTransit's own 3-30 s.
schedule=quick
docker inspect ecommerce-order --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null \
  | grep -qi '^Messaging__ReconnectQuickly=false$' && schedule=default
if [ -n "$ready_at" ]; then
  addresses="$(docker ps --format '{{.Names}}' | grep '^ecommerce-' | xargs docker inspect --format '{{.Name}} {{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' | sed 's#^/##')"
  accepted="$(docker logs --timestamps --since "$ready_at" e-commerce-rabbitmq 2>&1 | grep 'accepting AMQP connection' | sed -E 's/^([^ ]+) .*\(([0-9.]+):[0-9]+ ->.*/\1 \2/')"
  reconnect="$(PY=python3; command -v python3 >/dev/null && python3 -c '' 2>/dev/null || PY=python; "$PY" - "$ready_at" "$addresses" "$accepted" <<'PYTHON'
import json, sys
from datetime import datetime
ready, addresses, accepted = sys.argv[1:4]
t = lambda s: datetime.fromisoformat(s.rstrip('Z')[:26]).timestamp()
owner = {ip: line.split()[0] for line in addresses.splitlines() for ip in line.split()[1:]}
first = {}
for line in accepted.splitlines():
    stamp, ip = line.split()
    name = owner.get(ip)
    if name and name not in first:
        first[name] = round(t(stamp) - t(ready), 2)
print(json.dumps(dict(sorted(first.items()))))
PYTHON
)"
fi
grep -E 'ok   |FAIL|readings|checkout refused' "$log" | grep -v 'timings: ' | sed 's/.*msg="//; s/" source=console//' | sort | uniq -c | sort -rn | head -12

# Nothing faulted: a message in an error queue is work lost (#299 showed itself this way).
error_queues="$(docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages 2>/dev/null | awk '$1 ~ /_error$/ && $2 != 0 {printf "%s%s=%s", sep, $1, $2; sep=","}')"
passed=false
[ "$k6_exit" = 0 ] && [ -z "$error_queues" ] && passed=true

# What the fault did to the orders, from each order's placed and paid times (the teardown's "timings" line).
PY=python3; command -v python3 >/dev/null && python3 -c '' 2>/dev/null || PY=python
orders="$("$PY" - "$log" "$fault_at" "$recovered_at" "${ready_at:-}" <<'PYTHON'
import json, re, sys
from datetime import datetime
log, fault_at, recovered_at, ready_at = sys.argv[1:5]
ms = lambda t: datetime.fromisoformat(t.replace('Z', '+00:00')).timestamp() * 1000
line = next((l for l in open(log, encoding='utf-8', errors='replace') if 'timings: ' in l), None)
if line is None:
    print('null'); sys.exit()
raw = line.split('timings: ', 1)[1].split('" source=console', 1)[0].replace('\\"', '"')
pairs = json.loads(raw)
f, r = ms(fault_at), ms(recovered_at)
def stats(group):
    settle = sorted((p - c) / 1000 for c, p in group if p)
    q = lambda x: round(settle[min(len(settle) - 1, int(x * len(settle)))], 2) if settle else None
    return {'orders': len(group), 'paid': len(settle), 'settle_p50_s': q(.5), 'settle_p95_s': q(.95), 'settle_max_s': q(1)}
before = [p for p in pairs if p[0] < f]
during = [p for p in pairs if f <= p[0] < r]
after = [p for p in pairs if p[0] >= r]
paid_during = [p for c, p in during if p]
print(json.dumps({
    'before': stats(before), 'during': stats(during), 'after': stats(after),
    'backlog_cleared_after_recovery_s': round((max(paid_during) - r) / 1000, 2) if paid_during else None,
    # From when the broker really accepted connections, not from `docker start` (specs/154).
    'backlog_cleared_after_ready_s': round((max(paid_during) - ms(ready_at)) / 1000, 2) if paid_during and ready_at else None,
}))
PYTHON
)"
echo "orders: $orders"
[ -n "$ready_at" ] && echo "broker accepting connections at $ready_at; each service reconnected after (s): $reconnect"

cat > "$here/results/resilience-$fault-$run_id.timeline.json" <<JSON
{
  "fault": "$fault",
  "fault_for_seconds": $FAULT_FOR,
  "load_started_at": "$load_started_at",
  "fault_at": "$fault_at",
  "recovered_at": "$recovered_at",
  "healthy_at": "$healthy_at",
  "ready_at": "${ready_at}",
  "reconnected_after_ready_s": ${reconnect},
  "reconnect_schedule": "${schedule}",
  "k6_exit": $k6_exit,
  "error_queues": "$error_queues",
  "passed": $passed,
  "orders": ${orders:-null}
}
JSON
echo "error queues holding messages: ${error_queues:-none}"
echo "$([ "$passed" = true ] && echo PASSED || echo FAILED): $fault (k6 exit $k6_exit) - timeline kept in server/loadtest/results/resilience-$fault-$run_id.timeline.json"
rm -f "$log"
[ "$passed" = true ]
