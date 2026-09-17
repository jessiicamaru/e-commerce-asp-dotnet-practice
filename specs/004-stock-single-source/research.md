# Phase 0 Research: One Source of Truth for Stock

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-17

Six decisions, each with what was chosen, why, and what was rejected.

---

## D1 — What the announcement carries

**Decision**: `StockAvailabilityChangedEvent(Guid ProductId, int QuantityAvailable, bool IsAvailable, DateTime ObservedAt)`,
published by Inventory into `Ecommerce.Contracts/Inventory/`.

It carries the number **and** the derived flag, but Catalog stores only the flag.

**Rationale**: The consumer needs `ObservedAt` to reject stale announcements (D3) and `IsAvailable`
to act on. `QuantityAvailable` is included because the owner already has it and a future subscriber
— a low-stock alerter, a report — would otherwise need a second contract to get it. It costs four
bytes on a message that is already being sent.

Catalog **not storing** the number is the point of the feature and must survive review: storing it
recreates the thing being deleted, one release later and with a sync mechanism to make it look
defensible.

**Alternatives considered**:

- **Carry only `IsAvailable`.** Cleaner, and genuinely tempting. Rejected because it makes the
  contract single-purpose: the moment anything wants a threshold, the record changes shape, and a
  contract change is breaking for every service that deserializes it. Including the number now is
  cheaper than changing the record later.
- **Carry `QuantityOnHand` and `QuantityReserved` separately** and let subscribers derive. Rejected:
  it publishes Inventory's internal model, so a change to how availability is computed becomes a
  contract change. `StockItem.QuantityAvailable` is already derived rather than stored, precisely so
  a third number cannot disagree with the other two — the announcement should preserve that.

---

## D2 — Where the announcement is published from

**Decision**: From each of the six Application handlers that change stock, through the existing
`IPublishEndpoint`, staged **before** the single `SaveChangesAsync` — the shape
`ReserveStockCommandHandler` already uses. A small shared helper builds the event from a
`StockItem` so the six call sites cannot drift in what they send.

All six, read out of the code rather than reasoned about:

```bash
$ find server/src/Services/Inventory/Ecommerce.Inventory.Application -name "*CommandHandler.cs" | grep -v obj
.../Reservations/ConfirmStock/ConfirmStockCommandHandler.cs
.../Reservations/ExpireStock/ExpireStockCommandHandler.cs
.../Reservations/ReleaseStock/ReleaseStockCommandHandler.cs
.../Reservations/ReserveStock/ReserveStockCommandHandler.cs
.../Stock/Commands/RegisterProduct/RegisterProductCommandHandler.cs
.../Stock/Commands/SetStockOnHand/SetStockOnHandCommandHandler.cs
```

**Rationale**: This is the honest risk in the feature and it should be named rather than
engineered around. Six call sites is six chances to forget one, and a forgotten one is invisible —
the listing simply stops updating for that path, which nobody notices until a shopper buys something
that is gone. The mitigation is a test per path (D6), not cleverness.

Publishing before the single save is what makes the announcement atomic with the change it
describes, per constitution III. Publishing after would permit an announcement about a change the
database rejected, and a change nobody is told about.

**Alternatives considered**:

- **An EF Core `SaveChanges` interceptor** that inspects changed `StockItem` entries and publishes
  automatically. Strictly better on the "cannot forget" axis, and it was the first choice.
  **Rejected as unverified**: it would publish *during* `SavingChangesAsync`, and MassTransit's bus
  outbox writes its `OutboxMessage` through the same `DbContext`. Whether entities added to the
  change tracker mid-save are included in that same save is exactly the kind of thing that appears
  to work and then does not under load. Constitution V does not allow shipping that on the strength
  of it looking right. It is a good spike for later; it is not a thing to discover during this
  feature.
- **A domain-event list on `StockItem`, drained by the unit of work.** Same benefit, and it does not
  fight EF — but it introduces a pattern this repository does not use anywhere, for one entity.
  Rejected as disproportionate.
- **Publishing from the consumers/controllers instead of the handlers.** Rejected: it puts the
  announcement outside the transaction that made the change.

---

## D3 — How the consumer survives redelivery *and* reordering

**Decision**: A guarded update that compares the announcement's observation time against what is
already recorded:

```text
UPDATE products
   SET "Availability" = @isAvailable, "AvailabilityObservedAt" = @observedAt
 WHERE "Id" = @productId
   AND ("AvailabilityObservedAt" IS NULL OR "AvailabilityObservedAt" < @observedAt)
```

Zero rows affected means the announcement was a duplicate or was overtaken. Both are normal.

**Rationale**: FR-006 and FR-007 look like one requirement and are two. A plain
"only write if different" guard handles redelivery and **fails** reordering: an older
"out of stock" arriving after a newer "in stock" would overwrite it, and the product would read as
unavailable until something else moved. Worse in the other direction — an older "in stock" landing
last leaves the listing selling goods that are gone.

The comparison is in the `WHERE` clause for the same reason feature 003's status guard is: two
deliveries racing cannot both win, because the database decides.

**Alternatives considered**:

- **Trust the broker's ordering.** Rejected. RabbitMQ preserves order per queue only in the absence
  of redelivery, and redelivery is normal operation here — the whole reason FR-006 exists.
- **A monotonic version number on the stock item** instead of a timestamp. Marginally more correct
  (immune to clock adjustment) but needs a new column in Inventory and a contract field whose
  meaning is internal to the producer. `ObservedAt` comes from one service's clock, so it is
  comparable with itself, which is all the comparison requires.
- **`ORDER BY` semantics via MassTransit partitioning on `ProductId`.** Reduces reordering but does
  not remove it, and puts the guarantee in configuration — which constitution III rules out as the
  sole mechanism.

---

## D4 — What Catalog stores, and what a product with no announcement reads as

**Decision**: Two columns on `products` — `Availability` (boolean, **not null, default false**) and
`AvailabilityObservedAt` (nullable timestamp). `StockQuantity` is dropped.

A product with no announcement on record reads as **unavailable**.

**Rationale**: FR-005. The two ways to be wrong are not symmetrical. Showing "unavailable" for
something that is in stock costs a sale and is corrected by the next announcement. Showing
"available" for something that is gone takes an order, reserves nothing, and disappoints a customer
who has already decided to buy. Default to the recoverable error.

`AvailabilityObservedAt` being nullable is what makes the very first announcement for a product
win the comparison in D3 without a special case.

**Alternatives considered**:

- **A three-state `Unknown` / `InStock` / `OutOfStock`.** More honest internally, and rejected for
  the shopper-facing surface: "unknown" is not an answer anyone can act on, and it would have to
  render as one of the other two anyway. If it renders as available it is the dangerous default; if
  it renders as unavailable it is this decision with an extra state.
- **Nullable boolean, null meaning unknown.** Same objection, plus every read has to handle three
  cases forever.

---

## D5 — Breaking the product API now rather than adding alongside

**Decision**: Remove `StockQuantity` from `ProductResponse` and from `CreateProductCommand` in the
same change that adds `availability`. No deprecation period.

**Rationale**: There is no frontend in this repository and no external consumer — the blast radius
is the API surface itself and the two tests that touch it. Keeping `stockQuantity` alongside
`availability` "for compatibility" would leave the wrong number in the payload, which is the defect,
with a correct field beside it to make the wrong one look deliberate.

A field that is always wrong is worse than a field that is gone. A caller that breaks finds out
immediately; a caller reading a stale number never does.

**Alternatives considered**:

- **Keep `stockQuantity`, populate it from the announcement.** This is the rejected option from the
  spec's clarification, at the field level. It would mean publishing exact inventory counts — a
  business decision nobody has taken — and a count that is seconds stale reads as a bug in a way a
  boolean does not.
- **Deprecate over a release.** Rejected: deprecation is for consumers you cannot change. Here there
  are none.

---

## D6 — How this is tested

**Decision**: Two places.

1. **`Ecommerce.Inventory.Tests`** gains one test per publish site — six tests asserting that each
   of `ReserveStock`, `ReleaseStock`, `ConfirmStock`, `ExpireStock`, `SetStockOnHand` and
   `RegisterProduct` publishes an announcement carrying the availability the row ends up with. This
   is the mitigation for D2's "six chances to forget", and it is the part of this feature most worth
   the effort.
2. **`Ecommerce.Catalog.Tests`** — a new project, real PostgreSQL on 5433, mirroring
   `Ecommerce.Payment.Tests` including its bounded connection pool. It covers the consumer:
   redelivery, reordering, unknown product, and the unannounced-product default.

Both guarantees are **mutation-checked**: removing `AND ... < @observedAt` must fail the reordering
test, and removing the whole guard must fail the redelivery test. A test that passes with and
without the guarantee is not testing it.

**Rationale**: Constitution V, and the specific lesson from feature 003 — its queue-name collision
passed all fifteen unit tests, because the in-memory harness has no queue names to collide. The six
publish tests exist because the same class of omission (a path that silently stops announcing) is
invisible to every other kind of check.

**What is deliberately not covered, and should be read as unverified**: nothing in CI runs Catalog
and Inventory together against a real broker, so the announcement actually travelling from one to
the other is exercised only by hand (quickstart scenarios 1–3). This is the same gap feature 003
left open and it is the roadmap's Phase 7. It is named here rather than discovered later.

**Alternatives considered**:

- **Testing the consumer through the MassTransit harness and asserting on `Consumed`.** Rejected on
  experience: feature 003 did exactly this and the assertion returned false for messages that had
  been consumed perfectly well, because the harness's inactivity token had elapsed under a shared
  fixture. Assert on the row.
- **Adding the six publish assertions to the Catalog project instead.** Rejected: they are
  statements about Inventory's behaviour and belong with Inventory's database.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| A seventh path that changes stock is added later without an announcement | The listing silently stops updating for that path; a shopper buys something that is gone | Six tests name the existing paths, and D2 is written down so the obligation is discoverable. Not solved — an interceptor would solve it, and that is deferred as a spike |
| The 10-second window (FR-010) is treated as consistency | Something later reads Catalog's availability to make a sell/no-sell decision | Stated in the plan's Constitution Check as the line not to cross. Checkout still reserves under `FOR UPDATE` against Inventory |
| Products created before this feature never get an announcement | They read as unavailable forever | Accepted and in scope's exclusions. The next stock movement announces; `SetStockOnHand` on each is the manual fix. Worth saying out loud, because "deployed and the catalogue shows everything out of stock" otherwise looks like a failure |
| Clock skew between announcements | An older announcement wins the comparison | Bounded: `ObservedAt` is set by one service, so the comparison is against that service's own clock. It would only break across multiple Inventory instances, which do not exist |
