# Research: A consumer survives a transient database failure

## D1. Retry the message, not the statement

**Decision**: MassTransit's `UseMessageRetry`, configured before `UseEntityFrameworkOutbox`, so it wraps it: each
attempt opens a new transaction, a new inbox check and a new outbox.

**Rationale**:
- A serialization failure aborts the whole transaction; only re-running all of it is correct.
- EF's `EnableRetryOnFailure` cannot help here. It retries statements outside user transactions, and the outbox's
  consume *is* a user transaction. That is why Inventory logged "will be retried after 0ms" and then faulted anyway.

**Alternative rejected**: lowering the outbox's isolation to `ReadCommitted`. It would trade a visible failure for
anomalies the guarded updates were not designed against, and MassTransit's inbox deduplication relies on
`RepeatableRead`.

## D2. Only transient failures

**Decision**: retry when any exception in the chain is a PostgreSQL error with SQLSTATE `40001` (serialization) or
`40P01` (deadlock), or an Npgsql exception that marks itself transient (a lost connection). Read by property name
(`SqlState`, `IsTransient`), as Payment already does, so Shared does not depend on Npgsql.

**Rationale**: retrying a bug or a validation failure only delays its fault and multiplies its log lines.

## D3. The intervals

**Decision**: `Exponential(10, 20 ms, 2 s, 50 ms)`: up to ten more attempts, growing intervals, with MassTransit's
jitter.

**Rationale**: the losers of a serialization failure collided because they ran together. Jitter spreads them out.
Ten attempts within a few seconds outlast any burst the load test produced, and a failure that persists past that
belongs in the error queue.

## D4. Every service, one line

**Decision**: the six services with an EF outbox call `cfg.UseTransientRetry()` in their existing endpoint callback.
Identity and Cart add a callback for it.

**Rationale**: the defect was found in Inventory, but the cause - no retry anywhere - is in all of them. Order's
settlement, Catalog's read models and Activity's inserts hit the same class of failure under load.
