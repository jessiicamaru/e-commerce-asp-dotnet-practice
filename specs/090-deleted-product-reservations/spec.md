# Feature Specification: A deleted product's holds are released with its stock

**Feature Branch**: `090-deleted-product-reservations` | **Created**: 2026-09-27 | **Issue**: #181

**Status**: Merged (#189, 2026-09-27)

**Input**: Issue #181 - "deleting a product leaves its stock reservations behind".

## Why

Deleting a product (specs/024) publishes `ProductDeletedEvent` with every variant id, and Inventory's
`ForgetProductCommand` deletes those variants' `stock_items` rows. Their `stock_reservations` rows stay.

The code's comment says "Reservations cascade from the stock row". They do not: the model has **no foreign key**
from `stock_reservations` to `stock_items`, so nothing cascades. A **held** reservation stays `Held`, and the
expiry sweeper or the order's settlement still acts on it - "returning" units to a shelf that no longer exists.

The issue asked for one thing to be established first: what those paths do with no stock row. This record found:

| Path | With the stock row gone |
| :-- | :-- |
| Confirm (order completed) | skips the stock (`TryGetValue`), marks the reservation `Confirmed` |
| Release (order failed) | skips the stock, marks it `Released` |
| Expire (sweeper) | skips the stock, marks it `Expired` |
| Restock a cancelled order (specs/039) | skips the stock, marks it `Released` |
| Restock a returned parcel (specs/066) | skips the stock |

So nothing throws and no count goes wrong - but a hold whose variant is gone is still live, still found by the
sweeper and by settlements, and still claims units that exist nowhere.

## User Scenarios & Testing *(mandatory)*

### US1 - A deleted variant's held reservations end with it (Priority: P1)

When the catalogue deletes a product, every **held** reservation of its variants is released - status `Released`,
reason "Product deleted", settled now - in the same transaction that deletes their stock rows.

**Why this priority**: It is the defect: a live hold for something that no longer exists.

**Independent Test**: Stock a variant, reserve 3 for an order, delete the product: the reservation is `Released`
with the reason, and the order's settlement and the sweeper find nothing of it to act on.

**Acceptance Scenarios**:

1. **Given** a held reservation of a variant, **When** the product is deleted, **Then** the reservation is
   `Released`, `SettlementReason` "Product deleted", `SettledAt` set, and the stock row is gone.
2. **Given** that order then completes, **When** Inventory confirms it, **Then** there is nothing held to confirm.
3. **Given** the hold's deadline has passed, **When** the sweeper runs, **Then** it expires nothing of it.
4. **Given** an order holding a deleted variant **and** a kept one, **When** the product is deleted and the order
   completes, **Then** only the deleted variant's hold is released, and the kept variant is confirmed and moves its
   stock as usual.

---

### US2 - Settled reservations stay as the order's history (Priority: P2)

A reservation that already ended - confirmed, released or expired - is left exactly as it is. It records what the
order took, and every path that can still reach it skips a missing stock row.

**Why this priority**: The alternative - deleting reservations with the stock - would erase what an order took, and
changing a `Confirmed` row would claim units came back that did not.

**Independent Test**: Confirm an order, delete the product: the reservation is still `Confirmed` with its own
reason. Then cancel the order: the restock settles the reservation without throwing and without creating a stock row.

**Acceptance Scenarios**:

1. **Given** a confirmed reservation, **When** its product is deleted, **Then** it stays `Confirmed`.
2. **Given** that, **When** the paid order is cancelled (specs/039), **Then** the restock completes, the reservation
   ends `Released`, and no stock row is created for the deleted variant.

### Edge Cases

- **Redelivered `ProductDeletedEvent`.** The release is guarded on `Held`; a second delivery releases nothing and
  deletes nothing.
- **A reservation being made at the same moment.** Reserving locks the stock row (`FOR UPDATE`). Deleting the row
  waits for that lock, and the release is a later statement in the same transaction, so it sees the new hold and
  releases it. If the deletion wins the lock, the reservation finds no row and fails as "Unknown product".
- **A settlement in flight.** A confirm or release that read the hold before the deletion committed may still write
  its own terminal status afterwards (last writer wins on a settled row). Either ending is terminal and moves no
  stock that exists; accepted.
- **A product deleted before it was ever stocked.** No stock row and no reservation; nothing happens, as before.
- **Availability.** No announcement: the stock rows are gone, and Catalog has deleted the product.
- **The order itself.** Releasing Inventory's hold does not fail or cancel the order; what happens to an order for
  a deleted product is Order's business and unchanged (Out of scope).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `ForgetProductCommand` deletes the variants' stock rows and releases their `Held` reservations in
  **one transaction**.
- **FR-002**: A released reservation has `Status = Released`, `SettlementReason = "Product deleted"`
  (`ForgetProductCommandHandler.Reason`) and `SettledAt = now`.
- **FR-003**: Reservations in `Confirmed`, `Released` or `Expired` are not changed.
- **FR-004**: The release is one guarded statement (`WHERE "Status" = 'Held'`), so it is idempotent.
- **FR-005**: No new status, column, constraint or migration (specs/039's rule: nothing an earlier image cannot parse).
- **FR-006**: The repository's comment says what the code does.

### Key Entities

- **Stock reservation** (`stock_reservations`): one order's claim on one variant's units - `Held`, then `Confirmed`,
  `Released` or `Expired`. `ProductId` holds a variant id (specs/020).
- **Stock item** (`stock_items`): the variant's count, deleted with the product (specs/024).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a product is deleted, no reservation of its variants is `Held` - held by three tests that failed
  before the fix.
- **SC-002**: Settled reservations are unchanged by a deletion, and a later cancellation settles without a shelf -
  held by two tests.
- **SC-003**: Two mutations - no release, no `Held` guard - each turn a test red.
- **SC-004**: `Ecommerce.Inventory.Tests` passes in full against a real PostgreSQL.

## Decision

**Release the held ones, keep the settled ones - do not delete reservations.** The issue offered deleting them with
the stock row or keeping them as history with a marker, and pointed at specs/039's "Released with a reason". A held
reservation has nothing to hold, so it ends; `Released` with a reason is exactly how a cancelled order's holds end
today, and it is a status every image already parses. A settled reservation is the record of what an order took;
deleting it would erase that, and it is harmless because every path that reaches it skips a missing stock row (the
table above). Recorded as decided on the user's behalf ([research.md](research.md) D1).

## Assumptions

- A deleted variant never comes back: Catalog issues fresh ids, and a first variant reuses only its **own**
  product's id (specs/020), which a deleted product no longer has.
- `ProductDeletedEvent` carries every variant id of the product (specs/024), as today.

## Out of scope

- What Order does with an order for a product deleted between checkout and settlement.
- A foreign key from `stock_reservations` to `stock_items` (research D2).
