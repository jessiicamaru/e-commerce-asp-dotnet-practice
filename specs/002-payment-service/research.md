# Phase 0 Research: Payment Service

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-16

Six decisions, each with what was chosen, why, and what was rejected.

---

## D1 — How one order gets exactly one payment

**Decision**: A unique constraint on `OrderId` in `payments`, plus the MassTransit EF inbox. The
consumer reads any existing payment first; if the insert still loses a race, the unique violation is
caught, the winning row is re-read, and the reply reports **that** outcome.

**Rationale**: FR-005 and FR-006 are the same guarantee under different pressure — a redelivery, and
two deliveries at once. The read-first path handles the common case; the constraint handles the race
the read cannot see. Catching the violation and replying with the existing outcome matters: dropping
the message would leave the saga waiting forever, and replying with a fresh outcome could contradict
the one already recorded.

This is the pattern Inventory already uses (`(OrderId, ProductId)` unique), so it is familiar rather
than novel. Here the key is simply `OrderId`: a payment is per order, not per line.

**Alternatives considered**:

- **Inbox alone.** Rejected for the same reason as in feature 001 — it puts the whole guarantee in
  configuration that no test would notice regressing. Constitution III requires a constraint or a
  guarded update.
- **`SELECT ... FOR UPDATE` on an order row**, as Inventory does for stock. Rejected: there is no
  pre-existing row to lock. Inventory locks a `stock_item` that already exists; a payment is created
  by the very request being processed, so a unique constraint is the natural guard.

---

## D2 — How the outcome is chosen

**Decision**: A service-wide setting, `PAYMENT_OUTCOME`, bound to options and read per message.
Values `Approve` (the default) and `Reject`. An unrecognised value fails at startup rather than
being silently treated as one of them.

**Rationale**: This is what the user chose for FR-011. The saga's compensation branch —
`PaymentFailed` → `ReleaseInventoryCommand` → stock returned — has never run. Every order so far has
either succeeded or had its reservation quietly expire, so the branch is untested code guarding a
real failure mode.

Reading the setting per message rather than caching it at startup costs nothing and keeps the
behaviour obvious.

**Alternatives considered**:

- **A sentinel amount that always fails** (e.g. `666`). Rejected by the user. It would have allowed
  both paths in one running system without a restart, at the cost of a magic number that a future
  reader could hit by accident with a real order total.
- **No failure path at all**, exercising compensation by hand-publishing `PaymentFailedEvent`.
  Rejected: that keeps the compensation branch permanently outside CI.

---

## D3 — How "no money moved" is made impossible to miss

**Decision**: Three independent signals, because one is too easy to overlook:

1. Every payment row carries `Provider = "Stub"` — it is in the data, not only in the code.
2. The service logs a warning at startup naming itself a stand-in and stating the configured outcome.
3. `/health` reports both the stub nature and the configured outcome.

**Rationale**: FR-008 and FR-012. The entire risk of building a stand-in is that somebody later
mistakes it for the real thing — a deployment where every order is fulfilled without being paid for.
A comment in the source would not survive that; a column in every row and a line in every health
check might.

The health signal also solves a smaller, likelier problem: a service deliberately set to `Reject`
looks exactly like a broken one to whoever is debugging at 2am.

**Alternatives considered**:

- **Only a code comment and a README note.** Rejected — invisible at exactly the moment it matters.
- **Refusing to start outside Development.** Tempting, and safer. Rejected because this project runs
  everything in Production mode locally (no launch profile), so the guard would block ordinary use
  while teaching nothing.

---

## D4 — What the service does not verify

**Decision**: The amount is trusted as sent. The service does not check that the order exists, does
not recalculate the total, and does not verify the payer.

**Rationale**: It owns payments and nothing else (constitution I). Reaching into Order's database to
check a total would be exactly the cross-service coupling the architecture forbids, and asking Order
over HTTP would make a message consumer depend on another service being awake.

Recorded here because it is a real limitation rather than an oversight: **a wrong amount in the
command is charged as sent**. A real provider integration would still not fix that — the correct
place to guarantee it is wherever the command is built.

**Alternatives considered**:

- **Query the Order service to confirm the total.** Rejected: runtime coupling for a check that
  belongs upstream.
- **Reject payments for orders not seen in an `OrderSubmittedEvent`.** Rejected as scope creep: it
  would mean this service shadowing order state, which is the two-sources-of-truth problem again.

---

## D5 — Service and database layout

**Decision**: `Ecommerce.Payment`, four projects in the existing Clean Architecture split, HTTP on
**5061**, its own database on **5438**, routed through the gateway.

**Rationale**: Consistency with Catalog, Order and Inventory. Ports 5056–5060 and 5432–5437 are
taken, so 5061 and 5438 are next; both were confirmed free. Database-per-service is the project's
stated architecture and FR-009 restates it.

**Alternatives considered**:

- **A consumer-only worker with no HTTP.** Rejected: FR-007 needs a payment lookup and FR-012 needs
  health to report the configured outcome.
- **Folding payment into the Orchestrator.** Rejected: it would give the saga a second reason to
  change and make the orchestrator own business data.

---

## D6 — How this is tested

**Decision**: Extend the existing test project pattern — xUnit against a real PostgreSQL, with
MassTransit's test harness for the consumer. A second test project,
`Ecommerce.Payment.Tests`, mirroring `Ecommerce.Inventory.Tests`.

**Rationale**: SC-005 (50 simultaneous requests yield one payment) is a database-level guarantee:
the unique constraint is what enforces it, so an in-memory provider would prove nothing
(constitution V).

One lesson is carried over from feature 001 rather than relearned: the inventory concurrency test
was first written to open one connection per concurrent request, exceeded PostgreSQL's
`max_connections`, and failed as a connection error rather than on its assertion — while appearing
to pass on an earlier run. The payment fixture bounds its pool from the start.

**Alternatives considered**:

- **Reusing the inventory test project.** Rejected: a test project per service keeps the
  database-per-service boundary visible in the tests too.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| The stub is mistaken for a real payment service | Orders fulfilled with no money taken | D3's three signals; and this row, which is the honest statement that no amount of marking makes a stub safe to deploy |
| A wrong amount is charged as sent | Shopper charged the wrong total once a real provider exists | D4 records the limitation and names where the guarantee belongs |
| `PAYMENT_OUTCOME` left on `Reject` | Every checkout fails and looks like an outage | Reported at startup and in `/health`, per FR-012 |
