# Research: Checkouts of one product do not queue behind retries

## D1. Why RepeatableRead aborts the hot row

**Finding**: a reservation takes `SELECT ... FOR UPDATE` on the stock row. Under PostgreSQL's `REPEATABLE READ`, a
transaction waiting for that lock fails with `40001` when the transaction ahead of it updates the row and commits:
the new version is outside its snapshot. With every checkout on one product, every concurrent consume after the first
fails, and #299's retry turns that into a queue with backoff.

Under `READ COMMITTED` the same `FOR UPDATE` waits, then re-reads the latest committed row. That is what a reservation
needs: one at a time, each seeing the stock the previous one left.

## D2. ReadCommitted is what the code was written and tested for

**Finding**: `UnitOfWork.ExecuteInTransactionAsync` opens its own transaction at the database's default isolation
(`READ COMMITTED`), unless a transaction is already open. Handlers therefore run at `READ COMMITTED`:
- over HTTP;
- in every Inventory test, including the concurrency tests that prove no overselling;
- in the expiry sweeper.

Only inside a consumer do they join MassTransit's outbox transaction, at `REPEATABLE READ`. The isolation being
changed is the one no test covered.

## D3. The inbox at ReadCommitted

**Finding**: MassTransit's inbox deduplicates by the unique key of `InboxState` (message id, consumer id), and with
`UsePostgres()` locks that row `FOR UPDATE`. Neither depends on `REPEATABLE READ`: a redelivered message finds its row
and is skipped. The outbox's delivery service keeps its own transactions.

## D4. Inventory only

**Decision**: change Inventory, where the hot row is. Leave the other services at MassTransit's default until they
are measured.

**Rationale**: the measurement found one hot row. Changing isolation where nothing was measured would be a change
made without evidence (Principle V).

**Alternative rejected**: keep `RepeatableRead` and tune the retry. A shorter backoff only makes the same collisions
more frequent: every waiter on the hot row is still aborted once per predecessor.
