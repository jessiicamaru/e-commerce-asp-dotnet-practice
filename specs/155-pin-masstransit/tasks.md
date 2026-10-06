---
description: "Task list for MassTransit back on 8.3.6 after a broker outage stopped consumers for good"
---

# Tasks: MassTransit back on 8.3.6 after a broker outage stopped consumers for good

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [x] T001 Every `MassTransit*` reference 8.5.11 -> 8.3.6; nothing else from #326 moves
- [x] T002 `dependabot.yml`: MassTransit minors ignored, with the reason
- [x] T003 `MassTransitVersionTests`: every csproj and the resolved assembly on 8.3.x; a mutation to 8.5.11 caught
- [x] T004 Every suite passes on 8.3.6, `BrokerReconnectTests` included
- [x] T005 `fault.sh broker` three or more times on the rebuilt stack; no queue left with messages and no consumer
- [x] T006 Docs: reliable messaging, evaluation, resilience results, CLAUDE.md, timeline, backlog
- [ ] T007 Merged, closes #353

## Evidence

Verified 2026-10-06 on a stack rebuilt from this branch (images carry `MassTransit.RabbitMQ/8.3.6`), on a freshly
installed machine.

- **Build**: the solution builds on 8.3.6 with no code change; `dotnet list package --vulnerable --include-transitive`
  reports nothing.
- **Tests**: every suite passes - Inventory 89 (87 + the two new), Order 371, Catalog 268, Identity 289, Payment 53,
  Activity 52, Cart 22, ApiGateway 19, Orchestrator 18. In the first all-at-once run Orchestrator and Payment each lost
  one test to a connection opening against databases created seconds earlier; both passed in full when rerun alone.
- **Mutation**: `MassTransit.RabbitMQ` set to 8.5.11 in `Ecommerce.Cart.WebApi.csproj` fails
  `Every_project_references_MassTransit_8_3`, naming the project and the version; restored, it passes.
- **`fault.sh broker`, 5 of 5 passed** (60 s down at 3 checkouts/s): every order paid, none stuck, `reserved` 0, every
  error queue empty, and after each run **no queue with messages and no consumer**. Every service reconnected within
  1.8-4.4 s of the broker opening its port.

| Run | Placed / paid | Slowest reconnect after the port opened | Backlog cleared after the port opened |
| :-- | :-- | :-- | :-- |
| [12-04-01](../../server/loadtest/results/resilience-broker-2026-10-06T12-04-01.timeline.json) | 361 / 361 | 4.20 s | 72.09 s |
| [12-08-56](../../server/loadtest/results/resilience-broker-2026-10-06T12-08-56.timeline.json) | 336 / 336 | 4.39 s | 38.75 s |
| [12-12-29](../../server/loadtest/results/resilience-broker-2026-10-06T12-12-29.timeline.json) | 353 / 353 | 1.76 s | 28.20 s |
| [12-20-08](../../server/loadtest/results/resilience-broker-2026-10-06T12-20-08.timeline.json) | 361 / 361 | 3.06 s | 34.00 s |
| [12-23-33](../../server/loadtest/results/resilience-broker-2026-10-06T12-23-33.timeline.json) | 360 / 360 | 3.39 s | 31.78 s |

The first run's orders placed *before* the fault took ~100 s to settle: it was the stack's first load after being
created (cold databases, JIT), the "never judge right after a rebuild" of CLAUDE.md. Its outcome was the same.

Against #353: 0 of 5 here plus 0 of 7 in specs/154 on 8.3.6, 3 of 3 on 8.5.11.
