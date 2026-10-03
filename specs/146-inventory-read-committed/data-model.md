# Data Model: Inventory consumes without serialization aborts

No table, column or migration. What changes is how a consume's transaction sees the stock row:

```text
REPEATABLE READ:  T2 waits FOR UPDATE on row R; T1 commits R'  ->  T2 fails 40001  ->  retried after a backoff ...
READ COMMITTED:   T2 waits FOR UPDATE on row R; T1 commits R'  ->  T2 re-reads R' and proceeds
```
