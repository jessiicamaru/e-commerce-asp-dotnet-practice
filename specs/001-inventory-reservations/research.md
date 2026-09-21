# Phase 0 Research: Inventory Reservations

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-16

Seven decisions had to be settled before design. Each is recorded with what was chosen, why, and
what was rejected.

---

## D1 — How reservations avoid overselling under concurrency

**Decision**: Aggregated counter per product (`QuantityOnHand`, `QuantityReserved`) guarded by a
pessimistic row lock (`SELECT ... FOR UPDATE`) taken in a deterministic order within one
transaction.

**Rationale**: FR-007 demands that no interleaving lets reserved quantity exceed quantity on hand.
A row lock held for the duration of a short transaction gives that guarantee with one table and no
extra machinery. Orders at this stage arrive one at a time, so the serialization cost is irrelevant.
Locking rows in a fixed order (ascending product id) is what prevents deadlock when two orders
contain the same two products in opposite order — an easy bug to ship without it.

**Alternatives considered**:

- **Optimistic concurrency (`xmin` / `RowVersion`) with retry.** Rejected as the primary mechanism:
  under contention for a popular product it degrades into a retry storm, and the retry loop is
  extra code that has to be correct. It remains a reasonable fallback if lock waits ever show up in
  profiling.
- **Unit-row pool with `FOR UPDATE SKIP LOCKED`**, as described in
  [shopify-inventory-skip-locked-pattern.md](../../docs/concepts/shopify-inventory-skip-locked-pattern.md).
  Rejected **for now**, deliberately. It is the right answer for flash sales — thousands of buyers
  contending for one product — because writers never queue behind each other. The cost is one
  database row per physical unit, a replenishment worker to keep the pool stocked, and a much
  harder mental model. None of that is justified while the system has no traffic. The trigger to
  revisit is measurable lock contention on a single product, and the migration is contained: it
  changes how `StockItem` is stored, not the message contracts or the consumer's behaviour.

---

## D2 — How the saga tells inventory an order succeeded

**Decision**: Inventory consumes the existing `OrderCompletedEvent` and treats it as confirmation,
turning held units into a permanent deduction.

**Rationale**: This is a real gap, not a detail. The contracts in `Ecommerce.Contracts/Inventory`
cover reserve and release but **there is no confirm command**, and `OrderStateMachine` publishes
`OrderCompletedEvent` and then calls `Finalize()` — it never tells inventory the order went
through. Without handling this, every successful order leaves its units held forever: never
released, never deducted, and eventually swept away by expiry as if the order had failed. That
would silently re-sell goods that were already shipped.

`OrderCompletedEvent` already carries `OrderId`, already fires at exactly the right moment, and
requires no change to the saga. Consuming it keeps this feature additive.

**Alternatives considered**:

- **Add `ConfirmInventoryCommand` to the contracts and publish it from the saga.** More explicit,
  and arguably better long term because it separates "the order is done" from "inventory, commit".
  Rejected for now because it means editing the saga and the shared contracts for no behavioural
  gain — and a shared-contract change touches every service.
- **Treat reserved units as already deducted and never confirm.** Rejected: it makes released and
  confirmed indistinguishable, so the expiry sweeper cannot tell a stranded reservation from a
  completed one.

---

## D3 — How consumers stay idempotent

**Decision**: Two layers. MassTransit's Entity Framework **inbox** for transport-level duplicate
delivery, plus a **unique constraint on (OrderId, ProductId)** in the reservations table as the
business-level guard.

**Rationale**: FR-006 has to hold even if the inbox is ever misconfigured, a message is replayed
from a different deployment, or the saga genuinely re-sends. The inbox stops the same delivery
being processed twice; the unique constraint makes a second reservation for the same order and
product impossible regardless of how it arrives. Belt and braces is warranted here because the
failure mode — silently double-deducting stock — is invisible until someone counts the warehouse.

Reservation state transitions are written as guarded updates (`Held → Released` only when currently
`Held`), so a repeated release affects zero rows rather than crediting stock twice, satisfying
User Story 3 scenario 3 and FR-017.

**Alternatives considered**:

- **Inbox only.** Rejected: correct in theory, but it puts the entire guarantee in configuration
  that no test would catch if it regressed.
- **Application-level "have I seen this message id" table.** Rejected: that is what the inbox
  already is, hand-rolled and worse.

---

## D4 — How expiry is implemented

**Decision**: A hosted background service in the Inventory process that periodically finds
reservations still `Held` past their `ExpiresAt` and releases them, in small batches, inside a
transaction using the same locking order as D1.

**Rationale**: FR-016 wants stranded stock recovered without human intervention. A sweeper is the
simplest thing that does that, needs no scheduler infrastructure, and is trivially testable by
moving a clock. Batching bounds the transaction size so a large backlog cannot lock the table for
long.

The sweeper must take the same locks in the same order as the reserve path, otherwise it becomes a
deadlock source — this is the non-obvious part and the reason the two share one code path.

**Alternatives considered**:

- **MassTransit message scheduling (`Schedule`) with a delayed message per reservation.** Rejected:
  needs the RabbitMQ delayed-exchange plugin, which the compose file's image does not include, and
  it scatters the expiry rule across the saga.
- **Expire lazily when the row is next read.** Rejected: stock stays invisible until somebody
  happens to ask for that product, which defeats SC-007.

---

## D5 — How inventory learns that a product exists

**Decision**: Consume the existing `ProductCreatedEvent` from Catalog and register the product with
zero units on hand. Quantities are then set by staff through Inventory's own endpoint.

**Rationale**: This is what the user chose for FR-013 to FR-015 — inventory owns sellable quantity
outright. `ProductCreatedEvent` is already published through Catalog's transactional outbox and
already carries `ProductId`, so no producer changes are needed. It carries no quantity, which suits
the decision: the catalogue never sets stock.

Registering at zero means an order for a known-but-unstocked product is rejected for insufficient
stock rather than as an unknown product — the distinction the spec draws in its edge cases.

**Consequence to record**: `Product.StockQuantity` in Catalog is now descriptive only. It is **not**
removed by this feature (that would be a breaking API change), but nothing may read it for an
availability decision. This needs stating in the Catalog documentation, or the next developer will
reasonably assume it is authoritative.

**Alternatives considered**:

- **Catalog publishes stock changes and inventory mirrors them.** Rejected by the user: it leaves
  sell decisions resting on a copy.
- **Manual registration in inventory with no Catalog coupling.** Rejected: a product could be sold
  in the storefront while inventory has never heard of it.

---

## D6 — Service and database layout

**Decision**: A new four-project service `Ecommerce.Inventory` following the existing Clean
Architecture split, its own PostgreSQL database on host port **5437**, HTTP on **5060**, routed
through the gateway.

**Rationale**: Consistency with Catalog and Order is worth more than any saving from folding
inventory into an existing service, and database-per-service is the project's stated architecture.
Ports 5433 to 5436 are taken by the existing service databases and 5432 by a locally installed
PostgreSQL, so 5437 is the next free one; 5056 to 5059 are taken by the existing services, so 5060
is next.

**Alternatives considered**:

- **Put the consumers in the Catalog service.** Rejected: it would give Catalog two reasons to
  change and reintroduce the shared-stock-truth problem D5 exists to avoid.
- **A consumer-only worker with no HTTP API.** Rejected: FR-012 and FR-015 need endpoints to
  inspect and set stock, and a health endpoint is needed for the same reasons the other services
  have one.

---

## D7 — How this gets tested

**Decision**: Add a test project using MassTransit's `AddMassTransitTestHarness` for consumer
behaviour, with a real PostgreSQL for the concurrency and idempotency cases. Extend the CI smoke
job to cover the full saga path end to end.

**Rationale**: The repository currently has no unit tests at all — the only automated check is
`verify-auth.sh`. The success criteria here are precisely the kind that cannot be verified by hand:
SC-004 (no overselling across 100 concurrent orders) and SC-005 (replay changes nothing) are
concurrency properties. A harness test is the only honest way to claim them.

The test harness runs the consumer without a broker, so the behavioural tests are fast. The
concurrency test needs a real database, because the guarantee being tested *is* the database's
locking behaviour — an in-memory provider would prove nothing.

**Alternatives considered**:

- **Extend `verify-auth.sh` only.** Rejected: a shell script cannot express "run 100 of these at
  once and assert exactly 10 succeeded".
- **In-memory EF provider throughout.** Rejected: it does not implement row locking, so the tests
  most worth having would pass against broken code.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| Payment service still does not exist | The saga stops after inventory reserves; no `PaymentProcessed` ever arrives, so every reservation will expire and be released by the sweeper | Expected and correct behaviour for now. Worth stating plainly: this feature makes the saga progress one step further, not all the way. Verifying the confirm path (D2) requires publishing `PaymentProcessedEvent` by hand until a payment service exists |
| `Product.StockQuantity` remains in Catalog | A future change may treat it as authoritative again | Documented in D5; needs a note in the Catalog docs |
| Expiry period is a guess | Too short releases stock from live orders; too long strands it | Configurable, defaulting to 15 minutes; revisit once real payment timings are known |
