# Phase 1 Data Model: Order Lifecycle Visibility

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-16

**No new entities.** This feature changes how an existing row moves and how it is read, not what is
stored. The only schema change is an index.

---

## Entities as they already exist

### `orders`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | `uuid` PK | |
| `UserId` | `uuid`, required | The shopper who placed it. Written from the token at submission |
| `TotalAmount` | `numeric(18,2)`, required | |
| `Status` | `varchar(32)`, required | The enum name, not its number — `HasConversion<string>()` |
| `FailureReason` | `varchar(512)`, nullable | **Written for the first time by this feature** |
| `CreatedAt` | `timestamptz` | |
| `UpdatedAt` | `timestamptz` | Set on every settlement |

### `order_items`

Unchanged. Cascade-deleted with its order; carries `ProductId`, `ProductName`, `Quantity`,
`UnitPrice`, and a computed `TotalPrice` that is not persisted.

---

## Status transitions

`Status` is stored as the enum **name**, so any value written here must keep its spelling.

| From | Trigger | To | Also written |
| :--- | :--- | :--- | :--- |
| `Submitted` | `OrderCompletedEvent` | `Completed` | `UpdatedAt` |
| `Submitted` | `OrderFailedEvent` | `Failed` | `FailureReason`, `UpdatedAt` |

Every other combination is a no-op **by construction**, because the source state is in the `WHERE`
clause of the update (research D1). That includes:

- a second `OrderCompletedEvent` for an order already `Completed`;
- an `OrderFailedEvent` for an order already `Completed`, and the reverse;
- either event for an order id this database does not hold.

None of these raise an error. They affect zero rows, are logged, and the message is acknowledged.

### Reachability of `OrderStatus` — required by FR-012

| Value | Reachable? | By what |
| :--- | :--- | :--- |
| `Pending` | **No** | It is the field initialiser on the entity. `SubmitOrderCommandHandler` overwrites it with `Submitted` before the row is ever saved, so no row is persisted holding it |
| `Submitted` | Yes | Order submission |
| `StockReserved` | **No** | The saga reaches `InventoryReservedState`, but publishes nothing that announces it. Left unreachable by decision on 2026-09-16 — see the spec's Assumptions |
| `Paid` | **No** | Same: `PaymentProcessedEvent` goes to the saga, which announces only the completed order that follows |
| `Completed` | Yes | This feature |
| `Cancelled` | **No** | Belongs to a shopper-initiated cancellation that does not exist |
| `Failed` | Yes | This feature |

Four of seven values are unreachable, and that is written down here rather than left to be
rediscovered. **Do not delete them**: `Pending` is the entity's default and removing it changes
construction; the other three are the vocabulary the transitions above would extend into.

---

## Schema change

One migration. Nothing is dropped, nothing is backfilled, no data moves.

```text
CREATE INDEX "IX_orders_UserId_CreatedAt"
    ON orders ("UserId", "CreatedAt" DESC);
```

Declared through `OrderConfiguration` so the Fluent-API-only rule holds:

```csharp
builder.HasIndex(x => new { x.UserId, x.CreatedAt })
    .IsDescending(false, true)
    .HasDatabaseName("IX_orders_UserId_CreatedAt");
```

It serves the list query's filter and its ordering from one structure (research D6).

---

## Existing rows

Orders already sitting at `Submitted` whose sagas finished stay at `Submitted`. There is no backfill
and there should not be: the saga instances were finalized and removed, so their outcomes are no
longer recoverable, and a backfill would be inventing them. Any order submitted after this feature
ships settles normally.

---

## Transaction rules

- A settlement is **one statement**. It does not read the row first, and it stages nothing in the
  change tracker.
- The consumer publishes nothing, so there is no outbox sequencing to get right — the hazard
  constitution III exists for does not arise here. What does apply is its idempotency clause, and
  that is satisfied by the guard.
- The inbox (`InboxState`) participates in the same transaction as the update, so a message is
  marked consumed only if the update it caused commits.
- Both queries are read-only and take no transaction.
