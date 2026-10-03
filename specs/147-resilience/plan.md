# Implementation Plan: Checkout survives a service or the broker going down

**Branch**: `feat/291-resilience` | **Date**: 2026-10-04 | **Spec**: [spec.md](spec.md) | **Issue**: #291

## Summary

A k6 scenario, `resilience.js`, places checkouts at a steady rate and records each order without waiting for it.
Its teardown then waits for every order to settle and checks the invariants.

`fault.sh` runs it and injects the fault on a schedule:
1. start the load;
2. wait 15 s;
3. inject (stop Payment, stop RabbitMQ, restart the orchestrator, or pause Inventory);
4. hold;
5. recover;
6. let the load finish.

It writes a timeline beside k6's summary, and inspects the error queues once everything has settled.
`resilience_report.py` turns the kept files into `docs/testing/resilience-results.md`.

## Technical Context

**Language/Version**: bash, JavaScript (k6), Python 3
**Primary Dependencies**: the load-test library (specs/144); Docker; `rabbitmqctl`
**Storage**: none; summaries and timelines as JSON under `server/loadtest/results/`
**Testing**: the scenarios assert themselves; a negative control shows the check failing when a message is
deliberately left in an error queue
**Target Platform**: the compose stack on the developer's machine
**Project Type**: test tooling + documentation
**Constraints**: no service change to make a scenario pass (FR-005)
**Scale/Scope**: one k6 scenario, one driver, one report

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass, and under test.** Taking one service away must leave the others working: checkout keeps accepting orders while Payment, Inventory or the orchestrator is gone. |
| **II. Clean Architecture Layering** | **Not applicable.** No service code changes. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, and the point.** The broker outage is the outbox's test: an order written with its event must be delivered once the broker returns, and never twice in effect. |
| **IV. Identity Comes From the Token** | **Pass.** Customers act with their own tokens. |
| **V. Evidence Over Assumption** | **Planned.** Invariants checked through the API, error queues through the broker, a timeline per run, a generated report, and a negative control. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

```text
specs/147-resilience/                      the record
server/loadtest/resilience.js              steady checkouts, no waiting; teardown settles and checks
server/loadtest/fault.sh                   payment | broker | orchestrator | inventory
server/loadtest/resilience_report.py       -> docs/testing/resilience-results.md
server/loadtest/results/resilience-*.json  summaries and timelines
```

## Complexity Tracking

No violation to justify.
