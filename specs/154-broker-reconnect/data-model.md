# Data Model: Services reconnect to the broker within seconds of its return

No table, column or migration. What changes is the reconnect schedule of every receive endpoint:

```text
before:  lost -> 3 s, 6 s, 12 s, 24 s, 30 s, 30 s ... (+-3 s)   outbox waits for the slowest endpoint
after:   lost -> 1 s, 2 s, 4 s, 5 s, 5 s ...         (+-1 s)
```
