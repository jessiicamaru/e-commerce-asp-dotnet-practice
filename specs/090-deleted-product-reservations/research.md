# Research: A deleted product's holds are released with its stock

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27 | **Issue**: #181

---

## D1 - Release held reservations; keep settled ones

**Decision**: In `ForgetProductCommand`'s transaction, `UPDATE stock_reservations SET Status = 'Released',
SettledAt = now, SettlementReason = 'Product deleted' WHERE ProductId IN (...) AND Status = 'Held'`. Leave
`Confirmed`, `Released` and `Expired` rows alone.

**Rationale**:

- A held reservation claims units of a shelf that is gone, and is still found by the sweeper and by the order's
  settlement. Ending it removes the only rows anything would act on as a hold.
- `Released` with a reason is how specs/039 ended a cancelled order's holds, and it adds no status an earlier image
  cannot parse (the constitution's rollback rule).
- A settled reservation records what an order took. Every path that can still reach one - confirm, release, expire,
  both restocks - skips a missing stock row (`TryGetValue`), established by reading each handler and held by
  `A_cancellation_after_the_variant_was_deleted_settles_without_a_shelf_to_return_to`.

**Alternatives considered**:

- **Delete every reservation of the variants.** Rejected: erases an order's history, and the rows are harmless.
- **Delete only the held ones.** Rejected: a hold that disappears leaves no trace of why the order's line was never
  confirmed; a released row with a reason says so.
- **A new status `Orphaned`.** Rejected: an earlier image could not parse it (the specs/039 rule), and "released
  because the product was deleted" is already expressible.
- **Mark settled rows too.** Rejected: rewriting `Confirmed` as anything else would claim the units came back.

---

## D2 - No foreign key

**Decision**: Do not add a foreign key (with or without cascade) from `stock_reservations.ProductId` to
`stock_items.ProductId`.

**Rationale**: A cascade would delete the history D1 keeps. A restricting key would make deleting the stock row fail
while reservations exist - the product would stay in Inventory forever. `SET NULL` would need `ProductId` nullable, a
narrowing change for every reader and a migration an earlier image would have to survive. The release is one
statement and needs none of it.

**Alternatives considered**: the three key behaviours above, each rejected for the reason given.

---

## D3 - The stock rows first, then the release, in one transaction

**Decision**: `ExecuteInTransactionAsync` runs `ForgetAsync` (delete stock rows) and then `ReleaseHeldAsync`.

**Rationale**: `ReserveStock` locks the stock rows `FOR UPDATE` before inserting its holds. Deleting the rows waits for
that lock; the release is a separate statement that starts after the delete, so under READ COMMITTED it sees a hold
committed while the delete waited. The other way round, the release could run before that hold committed and miss it.
One transaction means the stock and the holds end together or not at all. `UnitOfWork` joins a transaction a consumer
already holds, as every other Inventory handler does.

**Alternatives considered**:

- **Two separate saves.** Rejected: a crash between them would leave the holds live with the stock gone - the defect.
- **Release first.** Rejected for the race above.

---

## D4 - Tests: the three paths that failed, and the two that already worked

**Decision**: Five tests in `ForgetProductTests`: a held reservation released with its stock; only the deleted
variant's hold in a mixed order; the sweeper finds nothing; a settled reservation unchanged; a later cancellation
settles without a shelf.

**Rationale**: The first three failed before the fix. The last two passed before it - they pin the behaviour the
decision relies on (settled rows are harmless), so a later change to a settlement handler that stops skipping a
missing row is caught here.

**Alternatives considered**: only the failing three. Rejected: D1's reasoning would then rest on reading code, not on
a test.
