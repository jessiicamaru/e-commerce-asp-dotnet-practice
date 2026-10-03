# Research: Load-test checkout and measure it

## D1. k6, from its container

**Decision**: k6 scripts, run by `docker run grafana/k6` attached to the compose network.

**Rationale**:
- k6 is scriptable in JavaScript, has thresholds that fail a run, and writes a machine-readable summary.
- The container needs nothing installed and reaches the gateway by its compose name.

**Alternatives rejected**:
- JMeter: XML plans and a JVM.
- Locust: Python, fine, but per-request percentiles in its summary are coarser, and it is one more runtime.
- NBomber: C# in the solution, but heavier to run by one command.

## D2. Setup bypasses the rate limit, the measurement does not

**Decision**: customers are registered through Identity directly (`http://identity:8080`). Every measured request
goes through the gateway.

**Rationale**: the gateway allows 30 sign-ins and registrations a minute per address (specs/062). A load generator is
one address. Lifting the limit for a test would test a configuration production never has.

## D3. Staff sign-in inside k6

**Decision**: TOTP computed in the script: base32 decode, then `k6/crypto` HMAC-SHA1 over the 30-second step. The code
is exchanged with the back office's `Origin`, waiting for the next window when this one's code was used.

**Rationale**: specs/110 and 138 make that the only way to act as staff; the verify scripts do the same in Python.

## D4. Invariants checked through the API

**Decision**: after the run, the scenario reads each order it placed, as its customer (`GET /api/orders/{id}`), until
none is `Submitted`. It reads the variant's stock (`GET /api/stock/{id}`) and asserts:
- paid quantity = starting on hand − on hand now;
- reserved = 0;
- (race) paid count = starting stock.

A k6 `Counter` of broken invariants with a threshold of 0 fails the run.

**Rationale**: no cross-service database access (Principle I), and the same path a person sees. `QuantityOnHand` and
`QuantityReserved`, never the derived `Available` alone (the lesson recorded in `verify-saga.sh`).

## D5. Latency reported, not asserted

**Decision**: thresholds only on `http_req_failed` (unexpected statuses), checks, and broken invariants.

**Rationale**: a laptop running 24 containers is not a server, and a latency threshold would make the scenario flaky
across machines while proving nothing about production.

## D6. The race shape

**Decision**: `shared-iterations` executor, 100 VUs, 100 iterations: every customer checks out exactly once, all
starting together after a barrier (setup finishes before any VU starts). Stock 20.

**Rationale**: the most contention the stack can be given, all on one stock row.

## D7. The report is generated

**Decision**: `report.py` reads the kept summaries and writes `docs/testing/load-test-results.md`: per scenario
throughput, request latency p50/p95/p99, settle time, error rate, and the invariant outcome, with the machine named.

**Rationale**: SC-003 - a number typed by hand into a report is a number nobody can check.
