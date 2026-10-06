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
| RabbitMQ stopped - before specs/154 | 63 s | 360 | 0 | 360 | 0 | empty | yes |
| RabbitMQ stopped - 1-5 s reconnect | 67 s | 361 | 0 | 361 | 0 | empty | yes |
| RabbitMQ stopped - 1-5 s reconnect | 64 s | 361 | 0 | 361 | 0 | empty | yes |
| RabbitMQ stopped - 1-5 s reconnect | 66 s | 360 | 0 | 360 | 0 | empty | yes |
| RabbitMQ stopped - MassTransit's 3-30 s reconnect | 67 s | 360 | 0 | 360 | 0 | empty | yes |
| RabbitMQ stopped - MassTransit's 3-30 s reconnect | 67 s | 361 | 0 | 361 | 0 | empty | yes |
| RabbitMQ stopped - MassTransit's 3-30 s reconnect | 68 s | 361 | 0 | 361 | 0 | empty | yes |
| RabbitMQ stopped - MassTransit's 3-30 s reconnect | 66 s | 361 | 0 | 361 | 0 | empty | yes |
| The orchestrator restarted | 7 s | 361 | 0 | 361 | 0 | empty | yes |
| Inventory hung | 31 s | 361 | 0 | 361 | 0 | empty | yes |

## How long each order waited

Orders are grouped by when they were placed: before the fault, during it, and after recovery. Settle time is from
placing the order to Order recording it Paid; "backlog cleared" is how long after recovery the last order placed
during the fault was paid.

| Fault | Placed before: settle p50 / p95 | During: orders, settle p50 / p95 / slowest | After: settle p50 / p95 | Backlog cleared after recovery |
| :-- | :-- | :-- | :-- | --: |
| Payment stopped | 0.21 s / 0.31 s | 185: 47.36 s / 68.58 s / 71.56 s | 9.08 s / 22.53 s | 25.05 s |
| RabbitMQ stopped - before specs/154 | 0.26 s / 0.69 s | 188: 100.30 s / 123.56 s / 127.46 s | 61.35 s / 74.26 s | 89.10 s |
| RabbitMQ stopped - 1-5 s reconnect | 0.87 s / 2.37 s | 200: 91.45 s / 112.87 s / 115.84 s | 58.96 s / 72.55 s | 84.51 s |
| RabbitMQ stopped - 1-5 s reconnect | 0.52 s / 1.41 s | 191: 132.44 s / 150.01 s / 154.82 s | 99.60 s / 113.48 s | 128.31 s |
| RabbitMQ stopped - 1-5 s reconnect | 8.43 s / 129.64 s | 199: 111.27 s / 128.14 s / 130.43 s | 94.56 s / 117.07 s | 127.81 s |
| RabbitMQ stopped - MassTransit's 3-30 s reconnect | - / - | -: - / - / - | - / - | - |
| RabbitMQ stopped - MassTransit's 3-30 s reconnect | 204.79 s / 214.57 s | 203: 185.74 s / 206.03 s / 208.87 s | 147.86 s / 159.39 s | 170.16 s |
| RabbitMQ stopped - MassTransit's 3-30 s reconnect | 1.09 s / 133.93 s | 205: 145.00 s / 162.07 s / 172.26 s | 125.96 s / 136.28 s | 146.85 s |
| RabbitMQ stopped - MassTransit's 3-30 s reconnect | 9.18 s / 118.35 s | 217: 109.40 s / 123.60 s / 141.07 s | 81.06 s / 91.94 s | 101.60 s |
| The orchestrator restarted | 0.20 s / 0.28 s | 19: 13.16 s / 17.41 s / 17.41 s | 0.77 s / 7.01 s | 12.15 s |
| Inventory hung | 0.21 s / 0.29 s | 91: 21.64 s / 31.94 s / 34.83 s | 0.28 s / 8.47 s | 19.61 s |

## The broker's return, step by step

From `docker start` to a drained backlog there are three steps, and only the middle one is the services'
(specs/154). **Port opened** is when RabbitMQ logged `started TCP listener on [::]:5672`; until then nothing can
connect, however fast it retries. **Every service back** is the slowest of the eight services' first
connection after that, read from the broker's log by the address it accepted. **Backlog cleared** is when the last
order placed during the outage was paid, counted from the port opening: the rest is draining the backlog.

| Run | Reconnect schedule | Port opened after start | Every service back within | Slowest | Backlog cleared after the port opened |
| :-- | :-- | --: | --: | :-- | --: |
| 2026-10-06 03:24 | 1-5 s | 26.7 s | 5.96 s (8 services) | catalog | 57.82 s |
| 2026-10-06 03:29 | 1-5 s | 53.5 s | 4.33 s (8 services) | payment | 74.85 s |
| 2026-10-06 03:35 | 1-5 s | 34.1 s | 3.85 s (8 services) | payment | 93.74 s |
| 2026-10-06 03:46 | MassTransit's 3-30 s | 80.3 s | 28.79 s (8 services) | payment | - |
| 2026-10-06 03:56 | MassTransit's 3-30 s | 74.2 s | 28.43 s (8 services) | payment | 96.00 s |
| 2026-10-06 04:02 | MassTransit's 3-30 s | 36.8 s | 30.70 s (8 services) | inventory | 110.04 s |
| 2026-10-06 04:08 | MassTransit's 3-30 s | 30.0 s | 6.36 s (8 services) | orchestrator | 71.59 s |

## What the runs showed

- **Nothing was lost, under any of the four faults.** Every order placed reached Paid, units sold equalled units
  deducted, nothing was left held and no message faulted. No customer saw an error either: checkout depends on
  Catalog, Cart and Identity synchronously, and on nothing that was taken away here.
- **The broker outage cost the most time, and for longest.** Checkout kept taking orders - each written with its
  event to Order's own database, the transactional outbox - but once RabbitMQ returned, the backlog took
  101.60 s to clear, and orders placed *after* recovery waited a median of
  81.06 s behind it. Payment's outage, of the same length, cleared in
  25.05 s. Why is in the table above (specs/154, #304): RabbitMQ itself takes
  about half a minute to open its port after it starts, the services were then waiting up to 30 s between
  reconnect attempts (MassTransit's schedule, now 1-5 s), and the rest is draining the backlog.
- **A hung service is not a dead one, and both recover.** Inventory frozen (connections open, nothing answered)
  and the orchestrator restarted both resumed from their queues and their own databases.
- **A harness lesson.** Cart removes what was ordered only when an order completes (specs/010), so a customer who
  orders again while an earlier order is held up by a fault can have the new line removed by the earlier
  order's completion. The runs give each customer enough time between orders that this cannot happen, so the
  errors counted above are the fault's alone.

## What each fault exercised

- **Payment stopped** (`docker stop ecommerce-payment, then start`): The saga holds each order in its "awaiting payment" state; its timeout (600 s) is longer than the outage.
- **RabbitMQ stopped** (`docker stop e-commerce-rabbitmq, then start`): Checkout writes each order and its OrderSubmittedEvent to Order's own database (the transactional outbox); every service reconnects and its outbox delivers when the broker returns.
- **RabbitMQ stopped** (`docker stop e-commerce-rabbitmq, then start`): Checkout writes each order and its OrderSubmittedEvent to Order's own database (the transactional outbox); every service reconnects and its outbox delivers when the broker returns.
- **RabbitMQ stopped** (`docker stop e-commerce-rabbitmq, then start`): Checkout writes each order and its OrderSubmittedEvent to Order's own database (the transactional outbox); every service reconnects and its outbox delivers when the broker returns.
- **RabbitMQ stopped** (`docker stop e-commerce-rabbitmq, then start`): Checkout writes each order and its OrderSubmittedEvent to Order's own database (the transactional outbox); every service reconnects and its outbox delivers when the broker returns.
- **RabbitMQ stopped** (`docker stop e-commerce-rabbitmq, then start`): Checkout writes each order and its OrderSubmittedEvent to Order's own database (the transactional outbox); every service reconnects and its outbox delivers when the broker returns.
- **RabbitMQ stopped** (`docker stop e-commerce-rabbitmq, then start`): Checkout writes each order and its OrderSubmittedEvent to Order's own database (the transactional outbox); every service reconnects and its outbox delivers when the broker returns.
- **RabbitMQ stopped** (`docker stop e-commerce-rabbitmq, then start`): Checkout writes each order and its OrderSubmittedEvent to Order's own database (the transactional outbox); every service reconnects and its outbox delivers when the broker returns.
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
| [`resilience-broker-2026-10-06T03-21-11.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-21-11.json) | [`resilience-broker-2026-10-06T03-21-11.timeline.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-21-11.timeline.json) |
| [`resilience-broker-2026-10-06T03-26-00.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-26-00.json) | [`resilience-broker-2026-10-06T03-26-00.timeline.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-26-00.timeline.json) |
| [`resilience-broker-2026-10-06T03-31-46.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-31-46.json) | [`resilience-broker-2026-10-06T03-31-46.timeline.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-31-46.timeline.json) |
| [`resilience-broker-2026-10-06T03-41-28.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-41-28.json) | [`resilience-broker-2026-10-06T03-41-28.timeline.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-41-28.timeline.json) |
| [`resilience-broker-2026-10-06T03-52-40.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-52-40.json) | [`resilience-broker-2026-10-06T03-52-40.timeline.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-52-40.timeline.json) |
| [`resilience-broker-2026-10-06T03-59-25.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-59-25.json) | [`resilience-broker-2026-10-06T03-59-25.timeline.json`](../../server/loadtest/results/resilience-broker-2026-10-06T03-59-25.timeline.json) |
| [`resilience-broker-2026-10-06T04-05-36.json`](../../server/loadtest/results/resilience-broker-2026-10-06T04-05-36.json) | [`resilience-broker-2026-10-06T04-05-36.timeline.json`](../../server/loadtest/results/resilience-broker-2026-10-06T04-05-36.timeline.json) |
| [`resilience-orchestrator-2026-10-03T14-10-07.json`](../../server/loadtest/results/resilience-orchestrator-2026-10-03T14-10-07.json) | [`resilience-orchestrator-2026-10-03T14-10-07.timeline.json`](../../server/loadtest/results/resilience-orchestrator-2026-10-03T14-10-07.timeline.json) |
| [`resilience-inventory-2026-10-03T14-12-48.json`](../../server/loadtest/results/resilience-inventory-2026-10-03T14-12-48.json) | [`resilience-inventory-2026-10-03T14-12-48.timeline.json`](../../server/loadtest/results/resilience-inventory-2026-10-03T14-12-48.timeline.json) |

Rerun: `server/loadtest/fault.sh payment|broker|orchestrator|inventory`, then `python server/loadtest/resilience_report.py`.
