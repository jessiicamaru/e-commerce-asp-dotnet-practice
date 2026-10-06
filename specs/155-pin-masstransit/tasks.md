---
description: "Task list for MassTransit back on 8.3.6 after a broker outage stopped consumers for good"
---

# Tasks: MassTransit back on 8.3.6 after a broker outage stopped consumers for good

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [ ] T001 Every `MassTransit*` reference 8.5.11 -> 8.3.6; nothing else from #326 moves
- [ ] T002 `dependabot.yml`: MassTransit minors ignored, with the reason
- [ ] T003 `MassTransitVersionTests`: every csproj and the resolved assembly on 8.3.x; a mutation to 8.5.11 caught
- [ ] T004 Every suite passes on 8.3.6, `BrokerReconnectTests` included
- [ ] T005 `fault.sh broker` three or more times on the rebuilt stack; no queue left with messages and no consumer
- [ ] T006 Docs: reliable messaging, evaluation, resilience results, CLAUDE.md, timeline, backlog
- [ ] T007 Merged, closes #353

## Evidence

(Filled in when the work is verified.)
