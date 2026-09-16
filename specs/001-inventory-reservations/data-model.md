# Phase 1 Data Model: Inventory Reservations

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

Two tables in `ecommerce_inventory_db`, plus the MassTransit inbox and outbox tables added by
`AddTransactionalOutboxEntities()`.

---

## `stock_items`

One row per product the inventory knows about. Created by the `ProductCreatedEvent` consumer (D5)
at zero units.

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK | Identity of the stock record |
| `ProductId` | uuid | Unique, not null | The catalogue product this tracks |
| `Sku` | varchar(50) | Not null | Copied from `ProductCreatedEvent`, for operator readability |
| `QuantityOnHand` | int | Not null, `>= 0` | Physical units held |
| `QuantityReserved` | int | Not null, `>= 0`, `<= QuantityOnHand` | Units promised to orders not yet settled |
| `CreatedAt` | timestamptz | Not null | |
| `UpdatedAt` | timestamptz | Not null | |

**Derived**: `QuantityAvailable = QuantityOnHand - QuantityReserved`. Not stored — a stored copy is
a third number that can disagree with the other two.

**Invariants** (FR-001, FR-007):

- `0 <= QuantityReserved <= QuantityOnHand`, enforced by a check constraint, not only in code. The
  database is the last line of defence against a concurrency bug, and the constraint is what turns
  a silent oversell into a loud failure.
- Satisfies SC-008: with nothing in flight, `QuantityReserved = 0` for every row.

**Indexes**: unique on `ProductId` (also the lookup path for reservation).

---

## `stock_reservations`

One row per (order, product). The unique constraint is the business-level idempotency guard from
D3.

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `Id` | uuid | PK | |
| `OrderId` | uuid | Not null, indexed | The order holding the units |
| `ProductId` | uuid | Not null | |
| `Quantity` | int | Not null, `> 0` | Units held by this line |
| `Status` | varchar(16) | Not null | `Held`, `Released`, `Confirmed`, `Expired` |
| `ExpiresAt` | timestamptz | Not null, indexed | When the sweeper may reclaim it |
| `CreatedAt` | timestamptz | Not null | |
| `SettledAt` | timestamptz | Null until settled | When it left `Held` |
| `SettlementReason` | varchar(512) | Nullable | Why it was released or expired |

**Constraints**:

- Unique on `(OrderId, ProductId)` — FR-006. A redelivered reserve command cannot create a second
  row, whatever happens upstream.
- Check `Quantity > 0` — FR-009.

**Indexes**: `(Status, ExpiresAt)` for the sweeper, so it never scans settled rows.

---

## State transitions

```text
                    ┌──────────┐
   reserve ────────▶│   Held   │
                    └────┬─────┘
                         │
      OrderCompletedEvent├────────────▶ Confirmed   (units leave QuantityOnHand permanently)
                         │
   ReleaseInventoryCmd   ├────────────▶ Released    (units return to available)
                         │
      sweeper, past TTL  └────────────▶ Expired     (units return to available)
```

`Held` is the only state with outgoing transitions. Every transition is written as a guarded update
that matches on the current status, so a second attempt affects zero rows instead of moving stock
again (FR-017, User Story 3 scenario 3, User Story 4 scenario 2).

| Transition | Effect on the stock item | Trigger |
| :--- | :--- | :--- |
| → `Held` | `QuantityReserved += Quantity` | `ReserveInventoryCommand`, all lines fit |
| `Held` → `Confirmed` | `QuantityReserved -= Quantity`, `QuantityOnHand -= Quantity` | `OrderCompletedEvent` |
| `Held` → `Released` | `QuantityReserved -= Quantity` | `ReleaseInventoryCommand` |
| `Held` → `Expired` | `QuantityReserved -= Quantity` | Sweeper, `ExpiresAt` passed |

Confirmation reduces both counters, which is what makes it different from release: the units are
gone from the building, not returned to the shelf.

---

## Transaction and locking rules

These are the rules that make the invariants hold; they are not incidental.

1. One reserve command is one transaction covering every line in the order. All-or-nothing
   (FR-003) is the transaction boundary, not application logic.
2. Within that transaction, lock the `stock_items` rows with `SELECT ... FOR UPDATE` **ordered by
   `ProductId` ascending**. Two orders containing the same two products in opposite order would
   otherwise deadlock.
3. Duplicate line items for the same product are summed before evaluation, so an order asking for
   3 and then 4 of one product is checked against 7 (spec edge case).
4. The expiry sweeper uses the same lock in the same order (D4).
5. The reservation rows and the outgoing reply are written in the same transaction as the stock
   change, through the transactional outbox — the pattern the rest of the repository already
   follows: stage, publish, then `SaveChangesAsync` once.
