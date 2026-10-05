# Data Model: A message delivered twice at once is consumed once and faults neither time

No table, column or migration. The table involved is MassTransit's own `InboxState` in every service with an outbox,
unique on `(MessageId, ConsumerId)` as `AK_InboxState_MessageId_ConsumerId`. What changes is what happens when two
consumes race for that key:

```text
before:  T1 inserts row ─ consumes ─ commits
         T2 inserts row ─ waits on T1 ─ 23505 ─────────────────▶ _error queue
after:   T1 inserts row ─ consumes ─ commits
         T2 inserts row ─ waits on T1 ─ 23505 ─ retried: T2' finds the row consumed ─▶ acknowledged, consumer not run
```
