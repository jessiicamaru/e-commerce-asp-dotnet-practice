---
description: "Task list for A message delivered twice at once is consumed once and faults neither time"
---

# Tasks: A message delivered twice at once is consumed once and faults neither time

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first.

- [x] T001 `InboxRedeliveryTests`: one message delivered twice at once through the real EF inbox; it fails before the fix with the production fault
- [x] T002 `TransientRetry.IsTransient`: a `23505` on `AK_InboxState_MessageId_ConsumerId` is transient
- [x] T003 `TransientRetryTests`: the inbox's `23505` counts, any other `23505` still does not
- [x] T004 Mutations; the Inventory, Payment and Order suites
- [x] T005 `fault.sh broker` on the rebuilt stack: every `_error` queue empty
- [x] T006 Docs: reliable messaging, CLAUDE.md, timeline, backlog
- [x] T007 Merged, closes #306 - #308

## Evidence

- **Reproduced before the fix**, against the real EF inbox on Payment's PostgreSQL (`InboxRedeliveryTests`). One message
  was delivered twice at once. The second delivery failed with the production fault:

  ```text
  receivefault DbUpdateException 23505: duplicate key value violates unique constraint "AK_InboxState_MessageId_ConsumerId"
  ```

  The test with the policy failed; the one without it showed the fault.
- **After the fix**: 2/2, and 5/5 repeated runs of the race. One consume, no fault.
- `TransientRetryTests`: 5/5.
  - The inbox's `23505` is transient, also when wrapped.
  - A plain `23505`, one on `IX_payments_OrderId`, and a `23503` on the inbox's name are not.
- Mutations:
  - **without the inbox case**: the race test fails, which is the pre-fix run above;
  - **every `23505` transient**: `TransientRetryTests` fails.
- Suites: Payment 53/53, Inventory 83/83, Order 371/371.
- **`fault.sh broker` twice on the rebuilt stack** (RabbitMQ down 60 s, 3 checkouts a second):
  - every order paid (360, then 361), nothing held, nothing stuck;
  - **every `_error` queue empty**, both times.

  No service logged an inbox-key collision during either run, so the race did not happen in them. It is intermittent:
  the four runs of #291 never hit it, and #292's run hit it once. These runs show the fix breaks nothing end to end;
  the real-inbox test is what proves the race is handled.
- The two runs' files are not in `loadtest/results/`, since the resilience report covers #291's runs.
