# Contracts: Checkout survives a service or the broker going down

No HTTP, message or gRPC change. The interface is the driver:

```text
server/loadtest/fault.sh payment|broker|orchestrator|inventory
```

| Environment | Default | Meaning |
| :-- | :-- | :-- |
| `FAULT_AFTER` | 15 | seconds of load before the fault |
| `FAULT_FOR` | 60 (inventory: 30) | seconds the fault lasts |
| `LOAD_FOR` | 120s | how long checkouts arrive |
| `RATE` | 3 | checkouts per second |

Exit 0 when:
- k6's thresholds held: every order settled, the invariants held, nothing faulted;
- and no `_error` queue holds a message.

The scenario uses only existing endpoints: those of `checkout.js` (specs/144), minus the settle polling inside an
iteration.
