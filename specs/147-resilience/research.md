# Research: Checkout survives a service or the broker going down

## D1. Docker as the fault injector

**Decision**:
- `docker stop` / `docker start` for Payment and RabbitMQ;
- `docker restart` for the orchestrator;
- `docker pause` / `docker unpause` for Inventory.

**Rationale**:
- A stopped container is a service that is down: connections refused.
- A paused one is a service that hangs: connections open, no answers. The issue asks for both shapes.
- No chaos tool is needed: four commands, one machine.

**Alternatives rejected**:
- Toxiproxy or Pumba: a network-level proxy is finer-grained, but adds a container in front of every service, and these
  four faults do not need it.
- Killing the process inside the container: the restart policy would bring it back at an uncontrolled time.

## D2. Place without waiting, settle in teardown

**Decision**: each iteration adds to cart and places the order, then returns. The teardown polls every customer's
orders until none is `Submitted` (up to `SETTLE_TIMEOUT`, 300 s), then checks the invariants.

**Rationale**:
- During an outage, orders cannot settle by design.
- If iterations waited, VUs would pile up and the arrival rate would collapse. The load would no longer be what the
  scenario claims to offer.

## D3. What "recovered" means, measured

**Decision**:
- The driver records UTC timestamps for when the load started, the fault was injected, and it was removed.
- The teardown records when the last order settled (`check_settled_at_ms`).
- Recovery time = last settled − fault removed.

**Rationale**: a single number a reader can compare across scenarios. It comes from two clocks on one machine.

## D4. Customer-facing errors are measured, not thresholds

**Decision**: the run fails on:
- a broken invariant;
- a stuck order;
- a message in an error queue.

Unexpected HTTP responses during the fault are counted and reported, but they do not fail the run.

**Rationale**:
- The guarantee under test is that nothing is lost.
- Whether a customer sees an error while, say, the broker is down is an observation the report should state, not hide
  behind a pass.

## D5. Error queues checked after the run

**Decision**: the driver lists queues with `rabbitmqctl` after the teardown and fails the run on any `_error` queue
with messages.

**Rationale**: a faulted message is lost work. It is how #299 showed itself.
