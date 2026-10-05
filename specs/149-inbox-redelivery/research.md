# Research: A message delivered twice at once is consumed once and faults neither time

## D1. How a duplicate faults instead of being skipped

**Finding**: MassTransit's EF inbox runs before the consumer, inside the consume's transaction:
1. it looks for the message's `InboxState` row by `(MessageId, ConsumerId)`;
2. if there is none, it inserts one;
3. it runs the consumer and marks the row consumed.

When two deliveries of one message run at once, neither finds a row, so both insert. The second insert waits on the
unique index until the first transaction commits, then fails with `23505` on `AK_InboxState_MessageId_ConsumerId`. The
failure comes from the inbox, not the consumer, so it is a **receive fault**. In RabbitMQ the message goes to its
`_error` queue.

Reproduced in `InboxRedeliveryTests` before the fix. Two deliveries of one message id, with a 500 ms consumer,
receive-observer output:

```text
pre 46.645            first delivery
pre 46.949            second delivery, while the first is consuming
receivefault DbUpdateException 23505: duplicate key value violates unique constraint "AK_InboxState_MessageId_ConsumerId"
postconsume 47.764    the first consumes once
```

**Decision**: try the losing delivery again. On its next attempt, a new transaction finds the row committed and
consumed, and the inbox acknowledges the message without running the consumer.

## D2. Recognise the inbox's key by its constraint name

**Decision**: transient means `SqlState == "23505"` **and** `ConstraintName == "AK_InboxState_MessageId_ConsumerId"`,
both read by property name. That is how the building block already reads `SqlState`, so it takes no dependency on
Npgsql.

**Alternatives rejected**:
- *Every `23505` transient*: a unique violation on the shop's own data is a decision, such as one payment per order
  or one review per customer. Retrying it ten times delays the fault and can hide a bug. The existing test asserts that
  a plain `23505` faults, and it still does.
- *A prefix such as `AK_InboxState_`*: the inbox has exactly one unique key besides its primary key, and the exact name
  is what the migrations create. If a future name were wrong, the real-inbox test would fail on it.
- *Catching it in each consumer*: the failure happens before any consumer runs, so no consumer can catch it.

## D3. Test against the real inbox, not a fake exception

**Decision**: `InboxRedeliveryTests` runs MassTransit's in-memory harness with `AddEntityFrameworkOutbox<PaymentDbContext>`
and `UseEntityFrameworkOutbox` on Payment's real PostgreSQL. One message is published twice with the same `MessageId`,
two at a time on the endpoint. A receive observer counts faults: the harness keeps one record per message id, so a
duplicate would hide behind its twin, as the first version of the test showed.

**Why**: a fake `PostgresException` would only restate the predicate. The claim is that the inbox, retried, drops the
duplicate, and only the real inbox can show that. The predicate itself is also covered in `TransientRetryTests`,
beside the cases of specs/145.

## D4. Why the resilience runs of #291 did not see it

The race needs two copies of one message in flight together. The four runs of #291 had none. #292's run had one, at
13:56:11, 57 s after the broker returned, while Order's outbox was delivering its 616-message backlog. It is
intermittent by nature. The test makes it deterministic, and `fault.sh broker` is the end-to-end check.
