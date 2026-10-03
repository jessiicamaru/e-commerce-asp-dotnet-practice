# Data Model: Checkout survives a service or the broker going down

No table, column or migration.

## The timeline file

`server/loadtest/results/resilience-<fault>-<run id>.timeline.json` sits beside the k6 summary
`resilience-<fault>-<run id>.json`:

| Key | Meaning |
| :-- | :-- |
| `fault` | `payment`, `broker`, `orchestrator` or `inventory` |
| `load_started_at` | UTC, ISO 8601 |
| `fault_at` | when the fault was injected |
| `recovered_at` | when it was removed |
| `error_queues` | `_error` queues holding messages after the run (empty when nothing was lost) |
| `passed` | k6's exit code was 0 and `error_queues` is empty |

## An order's life under a fault

```text
placed (Order + outbox) ──► broker ──► saga ──► Inventory reserves ──► saga ──► Payment ──► saga ──► Paid
  payment down:   waits at "Payment"   (the saga holds it; timeout 600 s)
  broker down:    waits in Order's outbox, and in each service's outbox, until the broker returns
  orchestrator:   waits in the saga's queue; its state is in the saga database
  inventory hung: waits in Inventory's queue
```
