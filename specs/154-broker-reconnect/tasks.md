---
description: "Task list for Services reconnect to the broker within seconds of its return"
---

# Tasks: Services reconnect to the broker within seconds of its return

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included.

- [ ] T001 `BrokerReconnect.ReconnectQuickly()`: the transport's filters, 1-5 s; false (with a log line) if it cannot apply
- [ ] T002 Every service's `UsingRabbitMq` calls it
- [ ] T003 `BrokerReconnectTests`: applied to a real RabbitMQ bus configuration; same exceptions handled and ignored; a mutation
- [ ] T004 `fault.sh broker` three times against the four baseline runs; the logs' retry intervals
- [ ] T005 Docs: reliable messaging, resilience results, the evaluation, CLAUDE.md, timeline, backlog
- [ ] T006 Merged, closes #304

## Evidence

(Filled in when the work is verified.)
