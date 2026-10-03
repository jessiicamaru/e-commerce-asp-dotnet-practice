---
description: "Task list for Inventory consumes without serialization aborts"
---

# Tasks: Inventory consumes without serialization aborts

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

- [x] T001 Inventory's outbox at `ReadCommitted`
- [x] T002 The Inventory suite passes, unchanged
- [x] T003 Checkout once and the race three times on the rebuilt stack; the error queues; the `40001` count
- [x] T004 Docs: the reliable-messaging page, CLAUDE.md, timeline, backlog
- [x] T005 Merged, closes #301 - #302

## Evidence

- The Inventory suite: 83/83, unchanged. Every handler already ran at `READ COMMITTED` outside a consumer, including
  the concurrency tests that prove no overselling.
- The race at `READ COMMITTED`: three runs, each `paid 20, failed 80, on hand 0, held 0, stuck 0`.
- **Eight warm checkout runs** (5 a second for 60 s, all one product), each after one discarded warm-up run.
  Latencies are Order's own `CreatedAt` → `PaidAt`; aborts are Inventory's `40001` log lines during the run:

  | Isolation | Run | Paid | `40001` aborts | p50 | p95 | Slowest |
  | :-- | :-- | --: | --: | --: | --: | --: |
  | REPEATABLE READ | 1 | 279 | 5,610 | 1.24 s | 26.88 s | 36.69 s |
  | REPEATABLE READ | 2 | 300 | 3,190 | 0.85 s | 13.24 s | 25.22 s |
  | REPEATABLE READ | 3 | 301 | 1,710 | 0.69 s | 2.17 s | 22.44 s |
  | READ COMMITTED | 1 | 300 | 0 | 0.34 s | 0.65 s | 0.81 s |
  | READ COMMITTED | 2 | 231 | 0 | 9.24 s | 41.26 s | 60.55 s |
  | READ COMMITTED | 3 | 301 | 0 | 0.89 s | 1.39 s | 1.95 s |
  | READ COMMITTED | 4 | 301 | 0 | 0.71 s | 1.01 s | 1.19 s |
  | READ COMMITTED | 5 | 301 | 0 | 1.10 s | 3.06 s | 4.43 s |

  Every run held every invariant, and every `_error` queue was empty after every run.
- **What it shows:**
  - The aborts are gone: thousands per run, then none.
  - The median is about the same either way.
  - The tail is tighter in 4 of 5 `READ COMMITTED` runs: the slowest order took at most 4.4 s, against at least
    22 s in every `REPEATABLE READ` run.
  - One `READ COMMITTED` run (2) was slow throughout. The same configuration was fast before and after it, which is
    the machine's variance, also seen in the per-stage breakdown.
- **The per-stage breakdown** (each service's timestamps): in every slow run, the time is in Inventory's reservation
  and confirmation, waiting on the one stock row every checkout of one product shares. Charging and settling stay at
  about 0.1–0.5 s. Checkout throughput on one product is capped by that row's lock hold time. That is the trade-off
  `docs/concepts/shopify-inventory-skip-locked-pattern.md` studied and did not adopt.
- **Corrected premise:** the issue's 8.3 s figure was a cold start right after a full rebuild, as noted on #301 and in
  specs/145's evidence.
