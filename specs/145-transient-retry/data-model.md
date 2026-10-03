# Data Model: A consumer survives a transient database failure

No table, column or migration: the fix is in how a message is consumed, not in what is stored.

## A message's life, before and after

```text
before:  consume (tx) ── 40001 ──► fault ──► <queue>_error          (the effect is lost)
after:   consume (tx) ── 40001 ──► rollback, wait 20-2000 ms ──► consume (new tx) ── ok ──► committed once
                                   ... up to 10 more attempts, then <queue>_error as before
         consume (tx) ── any other exception ──► fault at once, no retry
```
