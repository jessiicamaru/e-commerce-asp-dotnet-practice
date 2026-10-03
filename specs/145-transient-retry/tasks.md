---
description: "Task list for A consumer survives a transient database failure"
---

# Tasks: A consumer survives a transient database failure

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [x] T001 `Ecommerce.Shared/Messaging/TransientRetry.cs`: `IsTransient`, `UseTransientRetry`
- [x] T002 Every service's endpoint callback calls it before the outbox; Identity and Cart gain a callback
- [x] T003 Tests: a `40001` consumer retried to success; a non-transient one faults at once; `IsTransient` cases
- [x] T004 Mutation: the retry removed, and every exception treated as transient
- [x] T005 The checkout load run with empty error queues
- [x] T006 Docs: CLAUDE.md, reliable-messaging page, timeline, backlog
- [x] T007 Merged, closes #299 - #300

## Evidence

- Found by measurement: `server/loadtest/run.sh checkout` (#290, 5 checkouts a second for 60 s). Before the fix it read
  `paid 301, on hand 100000 → 99700, reserved 1`, with one `OrderCompletedEvent` in `OrderCompleted_error`. The fault
  headers read `Npgsql.PostgresException: 40001: could not serialize access due to concurrent update`,
  `MT-Fault-RetryCount = None`.
- `TransientRetryTests` (Inventory.Tests), 5/5, on MassTransit's test harness:
  - a consumer failing three times with `40001`, or with `40P01`, is consumed on the fourth attempt with no fault;
  - without the policy the first `40001` faults;
  - a non-transient failure faults at once (one attempt);
  - `IsTransient` refuses a unique violation, a timeout and an ordinary exception.
- Mutations, both caught:
  - the retry removed → the `40001`/`40P01` cases fail;
  - every exception counted transient → the non-transient and `IsTransient` cases fail.
- After the fix, the same checkout run on the rebuilt stack:
  - exit 0, every invariant held: 268 paid, 268 units deducted (100000 → 99732), 0 held, 0 stuck;
  - **every `_error` queue empty**;
  - Inventory logged 7,648 `40001` lines in the run: the conflicts still happen, and are now retried instead of lost.
- The cost, measured, and filed as #301:

  | Checkout run | Before | After |
  | :-- | --: | --: |
  | Settle time, median | 1.0 s | 8.3 s |
  | Settle time, p99 | 14.3 s | 40.3 s |
  | Checkouts completed | 301 | 268 (33 dropped, every VU busy) |

  `RepeatableRead` aborts every consume that waits on the hot stock row. Retrying makes them queue where losing made
  them fast and wrong. Making them fast and right (`ReadCommitted` for Inventory's consumes) needs its own design and
  measurement.
