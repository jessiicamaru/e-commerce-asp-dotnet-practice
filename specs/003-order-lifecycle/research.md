# Phase 0 Research: Order Lifecycle Visibility

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-16

Seven decisions, each with what was chosen, why, and what was rejected.

---

## D1 — How a settlement is made idempotent

**Decision**: A guarded update. The status is part of the `WHERE` clause, and the number of affected
rows is the answer:

```text
UPDATE orders
   SET "Status" = 'Completed', "UpdatedAt" = @now
 WHERE "Id" = @orderId
   AND "Status" = 'Submitted'
```

Expressed through EF Core's `ExecuteUpdateAsync`, which issues exactly this statement. Zero rows
affected means the order was already settled, or is not held here — both are non-errors.

**Rationale**: Constitution III asks for precisely this shape: *"State transitions MUST be written so
that a repeated attempt affects zero rows rather than applying the effect a second time."* The
database evaluates the guard atomically, so two deliveries racing each other cannot both win.

**Alternatives considered**:

- **Load the order, check `if (order.Status == Submitted)`, then `SaveChangesAsync`.** This is the
  obvious shape and it is wrong. Two concurrent deliveries can both read `Submitted` before either
  writes, and both then pass the check. The window is small and the failure is invisible in testing,
  which is exactly the profile of the two outbox-ordering bugs this repository has already shipped.
- **An optimistic concurrency token on the row** (`xmin` or a rowversion). Rejected: it converts the
  race into a `DbUpdateConcurrencyException` that must be caught and interpreted, needs a migration,
  and buys nothing the `WHERE` clause does not already give. It would also make *every* order write
  retry-prone, to solve a problem that exists on one transition.
- **A unique constraint**, as Inventory and Payment use. Rejected: there is no row being *inserted*
  here. Those services guard creation; this one guards a transition, and a transition's natural
  guard is its precondition.

---

## D2 — Telling "already settled" apart from "never heard of it"

**Decision**: When the guarded update affects zero rows, run one existence check and log
accordingly — `Information` when the order exists and was already settled, `Warning` when no such
order is held by this service. Either way the message is acknowledged, not retried.

**Rationale**: FR-006 requires the unknown-order case to be *visible to an operator*, and zero rows
alone cannot distinguish the two. The extra read happens only on the rare path — the common case is
one row affected and no second query.

Without the distinction, a database that had been reset and a broker redelivering normally would
produce identical log lines, and the operator reading them would learn nothing. That is the
"assertion the reader cannot check" failure mode, applied to logs.

**Alternatives considered**:

- **Log one line for both.** Rejected as above: cheap to write, useless to read.
- **Throw on an unknown order so the broker retries.** Rejected outright. The order is not going to
  appear later; retrying forever turns a stale message into a permanently faulted endpoint, and
  eventually a dead-letter queue nobody is watching.
- **Always load the order first and branch in C#.** Rejected — that is D1's rejected read-modify-write
  with an extra step.

---

## D3 — Where the inbox comes from

**Decision**: No migration. Add
`x.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<OrderDbContext>(context))`
to the Order service's MassTransit registration, matching Inventory and Payment.

**Rationale**: `OrderDbContext.OnModelCreating` already calls `AddTransactionalOutboxEntities()`, and
the tables are already in the initial schema. Verified rather than assumed:

```bash
$ grep -o "InboxState\|OutboxState\|OutboxMessage" \
    server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/20260903142425_InitialOrderSchema.cs \
  | sort -u
InboxState
OutboxMessage
OutboxState
```

So the storage for deduplication has been sitting in this database since the service was created;
only the wiring that makes consumers use it is missing — which is unsurprising, because until this
feature the service had no consumers at all.

**Alternatives considered**:

- **Rely on the guarded update alone and skip the inbox.** Defensible — D1 is the actual guarantee,
  and constitution III explicitly says idempotency must not rest on configuration. Rejected because
  the inbox costs one line here (the tables already exist) and keeps ordinary redeliveries from
  reaching the database at all. The important thing is the ordering of the two: the guard is the
  guarantee, the inbox is an optimisation. If the inbox were removed the tests must still pass.
- **A new migration to add inbox tables.** Rejected: they exist. Writing one would have produced an
  empty migration, or worse, a conflicting one.

---

## D4 — The shape of the order list

**Decision**: `GET /api/orders?page=1&pageSize=20`, 1-based, `pageSize` capped at 100, validated by
FluentValidation. The response carries the items plus `page`, `pageSize` and `totalCount`.

**Rationale**: FR-007 asks for pages *and* a total. Offset paging gives the total naturally; a
shopper's order count is measured in tens, so the usual objection to `OFFSET` does not apply at this
scale. The cap exists so one request cannot ask the service to materialise an unbounded result set.

**Alternatives considered**:

- **Keyset (cursor) paging on `CreatedAt`.** Faster at depth and stable under insertion, but it does
  not yield a total count without a second query, and it introduces an opaque cursor for a list that
  will rarely exceed one page. Rejected as premature.
- **No paging — return everything.** Rejected: it works until one shopper has a thousand orders, and
  then it fails in production rather than in review.

---

## D5 — How another shopper's order is hidden

**Decision**: The detail query filters on both the id **and** the owner:

```text
WHERE "Id" = @orderId AND "UserId" = @currentUserId
```

No match throws `NotFoundException`, which `GlobalExceptionHandler` renders as a 404.

**Rationale**: FR-010 requires a request for somebody else's order to be answered identically to a
request for one that does not exist. Filtering in the query makes that true by construction — there
is no state in which the handler holds another shopper's order and has to decide what to do with it.

**Alternatives considered**:

- **Load by id, compare `UserId`, throw `ForbiddenException`.** Rejected twice over. A 403 tells the
  caller the id is real, which is the disclosure FR-010 exists to prevent; and it loads the row
  before deciding, so the data has already left the database by the time authorization runs.
- **A row-level security policy in PostgreSQL.** Rejected: it would move an authorization rule out of
  the code that is reviewed and into a place nobody in this project looks, for a single-table filter.

---

## D6 — An index for the list query

**Decision**: One migration adding a composite index on `("UserId", "CreatedAt" DESC)` to `orders`.

**Rationale**: Every list request filters by `UserId` and sorts by `CreatedAt` descending. Without
the index that is a sequential scan plus a sort on every page of every shopper. The index matches the
query exactly, including the direction, so PostgreSQL can satisfy both the filter and the ordering
from it.

This is the only migration in the feature, and it changes no data — worth naming, because a migration
in the diff is what makes a change look risky when it is not.

**Alternatives considered**:

- **No index.** Rejected: the table only grows, and adding the index later means a migration anyway,
  taken under pressure.
- **An index on `UserId` alone.** Rejected: it serves the filter and leaves the sort to be done in
  memory, for the same migration cost.

---

## D7 — How this is tested

**Decision**: A third test project, `Ecommerce.Order.Tests`, xUnit against a real PostgreSQL on
5434, built on the same fixture pattern as `Ecommerce.Payment.Tests` — including the bounded
connection pool, which exists there because feature 001's concurrency test exhausted
`max_connections` and failed as a connection error while appearing to have passed on an earlier run.

The redelivery test is **mutation-checked**: with the `AND "Status" = 'Submitted'` guard removed, it
must fail. A test that passes both with and without the guarantee is not testing the guarantee.

**Rationale**: Constitution V — the guarantee under test is a `WHERE` clause the database evaluates,
so an in-memory provider would prove nothing about it. The same reasoning that put Inventory's
locking tests and Payment's unique-constraint tests on real PostgreSQL applies unchanged.

**What is deliberately not covered, and should be read as unverified**: the path from a real signed
token through `ICurrentUser` to the owner filter. The tests substitute `ICurrentUser` directly, so
they confirm the filter is applied to whatever identity is supplied — not that the identity supplied
at runtime is the right one. This is the same shape as the role-claim incident the constitution
cites: a check built from one's own assumption agreed with itself while every real token failed.
Closing it means adding an order read to `.github/scripts/verify-auth.sh`, which needs the Order
service and RabbitMQ in the smoke job. That is carried into `tasks.md` as an explicit task rather
than being quietly skipped.

**Alternatives considered**:

- **Add the tests to `Ecommerce.Payment.Tests`.** Rejected: a test project per service keeps the
  database-per-service boundary visible in the tests, and this one needs a different database.
- **Test the consumers through `AddMassTransitTestHarness` only, without a database.** Rejected: the
  harness would prove the consumer was invoked, which was never in doubt. What needs proving is what
  the second invocation does to the row.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| A shopper reads an order in the seconds between submission and settlement | It shows `Submitted`, which is correct but may read as "stuck" | Accepted, and named in the spec's edge cases. The intermediate statuses that would soften this were deliberately left unreachable |
| The owner filter is verified against a substituted identity, not a real token | An auth regression of the kind this project has already had could pass its tests | Recorded in the plan's Constitution Check and in `tasks.md`; the fix is an end-to-end smoke assertion, not another unit test |
| Orders already in the database stay `Submitted` forever | The eleven existing rows never settle, because their sagas finalized long ago | Accepted. No backfill: the saga state is gone, so any backfill would be guessing at outcomes. Worth saying out loud, since "the fix is deployed and the old rows are still wrong" otherwise looks like a failure |
