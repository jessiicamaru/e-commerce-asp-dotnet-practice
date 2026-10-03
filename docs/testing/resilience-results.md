# Resilience results

> **Generated** by [`server/loadtest/resilience_report.py`](../../server/loadtest/resilience_report.py) from the
> runs kept in [`server/loadtest/results/`](../../server/loadtest/results/). Do not edit by hand - run
> `server/loadtest/fault.sh` and the script again (specs/147).

## What was done

For each fault, customers checked out at a steady rate through the gateway while one part of the system was taken
away and brought back ([`fault.sh`](../../server/loadtest/fault.sh), [`resilience.js`](../../server/loadtest/resilience.js)).
Afterwards the run waited for every order to settle and checked, through the API, that every order was terminal,
that units sold equalled units deducted and that nothing was held - and, through the broker, that no message had
faulted into an error queue. A run fails on any of those, never on an error a customer saw while something was
down: that is measured and reported below.

Everything ran on one machine, as Docker reported it: **12 CPUs, 7.5 GiB memory, Docker Desktop, Docker 29.0.1** - the whole stack and k6 together. Payment was
the stub approving every payment.

## Results

| Fault | Down for | Orders placed | Customer errors | Paid | Held after | Error queues | Passed |
| :-- | --: | --: | --: | --: | --: | :-- | :-- |
| Payment stopped | 62 s | 361 | 0 | 361 | 0 | empty | yes |
| RabbitMQ stopped | 63 s | 360 | 0 | 360 | 0 | empty | yes |
| The orchestrator restarted | 7 s | 361 | 0 | 361 | 0 | empty | yes |
| Inventory hung | 31 s | 361 | 0 | 361 | 0 | empty | yes |

## How long each order waited

Orders are grouped by when they were placed: before the fault, during it, and after recovery. Settle time is from
placing the order to Order recording it Paid; "backlog cleared" is how long after recovery the last order placed
during the fault was paid.

| Fault | Placed before: settle p50 / p95 | During: orders, settle p50 / p95 / slowest | After: settle p50 / p95 | Backlog cleared after recovery |
| :-- | :-- | :-- | :-- | --: |
| Payment stopped | 0.21 s / 0.31 s | 185: 47.36 s / 68.58 s / 71.56 s | 9.08 s / 22.53 s | 25.05 s |
| RabbitMQ stopped | 0.26 s / 0.69 s | 188: 100.30 s / 123.56 s / 127.46 s | 61.35 s / 74.26 s | 89.10 s |
| The orchestrator restarted | 0.20 s / 0.28 s | 19: 13.16 s / 17.41 s / 17.41 s | 0.77 s / 7.01 s | 12.15 s |
| Inventory hung | 0.21 s / 0.29 s | 91: 21.64 s / 31.94 s / 34.83 s | 0.28 s / 8.47 s | 19.61 s |

## What the runs showed

- **Nothing was lost, under any of the four faults.** Every order placed reached Paid, units sold equalled units
  deducted, nothing was left held and no message faulted. No customer saw an error either: checkout depends on
  Catalog, Cart and Identity synchronously, and on nothing that was taken away here.
- **The broker outage cost the most time, and for longest.** Checkout kept taking orders - each written with its
  event to Order's own database, the transactional outbox - but once RabbitMQ returned, the backlog took
  89.10 s to clear, and orders placed *after* recovery waited a median of
  61.35 s behind it. Payment's outage, of the same length, cleared in
  25.05 s. Why the outbox drains this slowly is not yet established:
  [#304](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/304).
- **A hung service is not a dead one, and both recover.** Inventory frozen (connections open, nothing answered)
  and the orchestrator restarted both resumed from their queues and their own databases.
- **A harness lesson.** Cart removes what was ordered only when an order completes (specs/010), so a customer who
  orders again while an earlier order is held up by a fault can have the new line removed by the earlier
  order's completion. The runs give each customer enough time between orders that this cannot happen, so the
  errors counted above are the fault's alone.

## What each fault exercised

- **Payment stopped** (`docker stop ecommerce-payment, then start`): The saga holds each order in its "awaiting payment" state; its timeout (600 s) is longer than the outage.
- **RabbitMQ stopped** (`docker stop e-commerce-rabbitmq, then start`): Checkout writes each order and its OrderSubmittedEvent to Order's own database (the transactional outbox); every service reconnects and its outbox delivers when the broker returns.
- **The orchestrator restarted** (`docker stop ecommerce-orchestrator, then start`): The sagas in flight are rows in the saga database; their next messages wait in their queues.
- **Inventory hung** (`docker pause ecommerce-inventory, then unpause`): A frozen process holds its connections and answers nothing; reservations wait in its queue.

## How the check was checked

A message placed by hand in `OrderCompleted_error` before a run made that run fail with
`error queues holding messages: OrderCompleted_error=1`, and an expected stock one unit off fails the load
scenarios' invariant (specs/144) - the checks are able to fail.

## Runs

| Summary | Timeline |
| :-- | :-- |
| [`resilience-payment-2026-10-03T14-01-58.json`](../../server/loadtest/results/resilience-payment-2026-10-03T14-01-58.json) | [`resilience-payment-2026-10-03T14-01-58.timeline.json`](../../server/loadtest/results/resilience-payment-2026-10-03T14-01-58.timeline.json) |
| [`resilience-broker-2026-10-03T14-05-38.json`](../../server/loadtest/results/resilience-broker-2026-10-03T14-05-38.json) | [`resilience-broker-2026-10-03T14-05-38.timeline.json`](../../server/loadtest/results/resilience-broker-2026-10-03T14-05-38.timeline.json) |
| [`resilience-orchestrator-2026-10-03T14-10-07.json`](../../server/loadtest/results/resilience-orchestrator-2026-10-03T14-10-07.json) | [`resilience-orchestrator-2026-10-03T14-10-07.timeline.json`](../../server/loadtest/results/resilience-orchestrator-2026-10-03T14-10-07.timeline.json) |
| [`resilience-inventory-2026-10-03T14-12-48.json`](../../server/loadtest/results/resilience-inventory-2026-10-03T14-12-48.json) | [`resilience-inventory-2026-10-03T14-12-48.timeline.json`](../../server/loadtest/results/resilience-inventory-2026-10-03T14-12-48.timeline.json) |

Rerun: `server/loadtest/fault.sh payment|broker|orchestrator|inventory`, then `python server/loadtest/resilience_report.py`.
