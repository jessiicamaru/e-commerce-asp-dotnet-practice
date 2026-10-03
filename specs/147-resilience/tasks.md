---
description: "Task list for Checkout survives a service or the broker going down"
---

# Tasks: Checkout survives a service or the broker going down

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the scenarios assert themselves.

- [x] T001 `resilience.js`: steady checkouts without waiting; the teardown settles every order (long timeout), records when, and checks the invariants
- [x] T002 `fault.sh`: load, fault, recover; the timeline; the error queues after; the exit code
- [x] T003 Run payment, broker, orchestrator and inventory; any defect found gets its own issue
- [x] T004 The negative control
- [x] T005 `resilience_report.py` and `docs/testing/resilience-results.md`; testing strategy, docs index, CLAUDE.md, timeline, backlog
- [x] T006 Merged, closes #291 - #305

## Evidence

- Four runs on main's code: 3 checkouts a second for 120 s, the fault 15 s in. Each run's summary and timeline is in
  `server/loadtest/results/`; the report is `docs/testing/resilience-results.md`.

  | Fault | Down | Orders | Customer errors | Paid | Held | Error queues | Placed during: median wait | Backlog cleared after recovery |
  | :-- | --: | --: | --: | --: | --: | :-- | --: | --: |
  | Payment stopped | 62 s | 361 | 0 | 361 | 0 | empty | 47.4 s | 25.1 s |
  | RabbitMQ stopped | 63 s | 360 | 0 | 360 | 0 | empty | 100.3 s | 89.1 s |
  | Orchestrator restarted | 7 s | 361 | 0 | 361 | 0 | empty | 13.2 s | 12.2 s |
  | Inventory hung (paused) | 31 s | 361 | 0 | 361 | 0 | empty | 21.6 s | 19.6 s |

  Every run passed. Orders placed before each fault settled at a median of about 0.2 s.
- Negative control: a message planted in `OrderCompleted_error` before a short run made it fail with
  `error queues holding messages: OrderCompleted_error=1`. The run's files were removed, since it was not a
  measurement.
- Found and filed: **#304**. After the broker outage, the backlog took 89 s to clear for 63 s of outage, and orders
  placed after recovery waited a median of 61 s (0.26 s before). The cause is not established; candidates are listed on
  the issue.
- A harness lesson, recorded in CLAUDE.md and the report:
  - The first Payment run counted 10 "cart is empty" refusals that were not the fault's.
  - Cart removes ordered lines on completion (specs/010), so a customer re-ordering during the outage lost the new line
    to the earlier order's completion.
  - `fault.sh` now gives each customer `rate × (fault + 20 s)` of room between orders. The reruns had zero such errors.
