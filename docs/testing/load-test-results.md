# Load-test results

> **Generated** by [`server/loadtest/report.py`](../../server/loadtest/report.py) from the summaries kept in
> [`server/loadtest/results/`](../../server/loadtest/results/). Do not edit by hand - run the scenarios and the
> script again (specs/144).

## What was measured, and on what

Every run below used the same machine, as Docker reported it: **12 CPUs, 7.5 GiB memory, Docker Desktop, Docker 29.0.1**. The whole stack - 8
services, the gateway, both apps, 8 PostgreSQL databases, RabbitMQ, Seq, Mailpit and SeaweedFS - ran on it as
containers, and so did k6. These are therefore numbers for a laptop sharing itself between the load and the
system under it: they show the shape (which step is slow, whether the guarantees hold under contention), not what
a server would do. Latencies are milliseconds, measured by k6 at the gateway.

Payment was the stub approving every payment, so settle time is the saga itself: reserve, charge, settle.

## Many customers race for the last units

100 customers each check out one unit of a variant that has 20, all starting at once ([`race.js`](../../server/loadtest/race.js)). Every order must settle, exactly as many as the stock must be paid, the rest must fail for lack of stock, and nothing
may stay held. The readings are taken through the API after the run.

| Run | Placed | Paid | Failed | Stuck | Stock before | On hand after | Held after | Invariants |
| :-- | --: | --: | --: | --: | --: | --: | --: | :-- |
| 2026-10-03 12:05 UTC | 100 | 20 | 80 | 0 | 20 | 0 | 0 | held |
| 2026-10-03 12:06 UTC | 100 | 20 | 80 | 0 | 20 | 0 | 0 | held |
| 2026-10-03 12:07 UTC | 100 | 20 | 80 | 0 | 20 | 0 | 0 | held |

Time from placing the order to its outcome (Paid or Failed), under that contention:

| Run | Median | p95 | p99 | Slowest |
| :-- | --: | --: | --: | --: |
| 2026-10-03 12:05 UTC | 26,483 | 28,623 | 29,081 | 29,096 |
| 2026-10-03 12:06 UTC | 4,818 | 8,462 | 8,928 | 8,987 |
| 2026-10-03 12:07 UTC | 5,380 | 7,067 | 7,285 | 7,479 |

Request latency, last run:

| Step | Median | p95 | p99 | Slowest |
| :-- | --: | --: | --: | --: |
| add to cart | 211 | 374 | 414 | 427 |
| quote | 231 | 337 | 369 | 442 |
| place order | 329 | 484 | 503 | 503 |
| read order | 59.0 | 220 | 289 | 435 |

## Customers check out at a steady rate

Checkouts started at a constant **5 per second for 60s**, each by one of 60 customers: add to cart, ask for a quote, place the order, follow it until it settles ([`checkout.js`](../../server/loadtest/checkout.js)). Every checkout is of the same product, so every reservation and confirmation takes the lock on the same stock row - the hardest case for the shop.

| Run | Checkouts | Paid | Units deducted | Held after | Unexpected responses | Settle p50 | p95 | p99 | Slowest | Invariants |
| :-- | --: | --: | --: | --: | --: | --: | --: | --: | --: | :-- |
| 2026-10-03 12:08 UTC | 300 | 300 | 300 | 0 | 0.00% | 1,082 | 2,972 | 3,666 | 4,131 | held |
| 2026-10-03 12:10 UTC | 301 | 301 | 301 | 0 | 0.00% | 2,247 | 8,995 | 10,968 | 13,204 | held |
| 2026-10-03 12:11 UTC | 301 | 301 | 301 | 0 | 0.00% | 1,060 | 1,581 | 2,067 | 2,139 | held |

Settle time is from placing the order to reading it Paid, polled every half second, so it includes up to
0.5 s of polling. Request latency, last run:

| Step | Median | p95 | p99 | Slowest |
| :-- | --: | --: | --: | --: |
| add to cart | 38.5 | 68.2 | 88.6 | 206 |
| quote | 29.3 | 61.8 | 93.8 | 165 |
| place order | 65.0 | 110 | 157 | 181 |
| read order | 11.1 | 31.2 | 49.2 | 85.2 |

## Shoppers browse the catalogue

Anonymous shoppers ramp to **50 at once** (30s up, 60s hold, 10s down), each listing a page, searching and opening a product, then reading for a second ([`browse.js`](../../server/loadtest/browse.js)). Run 2026-10-03 12:13 UTC.

- **11,104 requests** in 102 s, 109.0 a second on average; unexpected responses 0.00%.

| Step | Median | p95 | p99 | Slowest |
| :-- | --: | --: | --: | --: |
| list products | 27.7 | 101 | 136 | 257 |
| search | 26.0 | 69.3 | 100.0 | 188 |
| one product | 15.2 | 40.5 | 61.0 | 244 |

## What the load tests found

The first runs of these scenarios found two things no test had (specs/145, specs/146):

1. **A paid order's stock confirmation was lost under load** ([#299](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/299)).
   The EF outbox consumes at `REPEATABLE READ`. Concurrent confirmations of one stock row were aborted with a
   serialization failure (`40001`), and no service retried a message, so one `OrderCompletedEvent` went to the
   error queue: 301 paid, 300 units deducted, 1 held for the expiry sweeper to resell. Fixed by a transient retry
   on every consumer, before the outbox.
2. **Every concurrent consume of a popular product was aborted and retried** ([#301](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/301)):
   1,710-5,610 `40001` per run. Inventory now consumes at `READ COMMITTED`, where a waiter re-reads the row and
   proceeds - zero aborts, the same median, a tighter tail over eight warm runs. The runs above are on that code.

A corrected claim belongs here too: a run straight after rebuilding every container was first read as the cost
of the retry. It was a cold start - see below.

## How to read it

- **The guarantee is the result that matters.** Under 100 simultaneous checkouts for 20 units the shop sold
  exactly 20, every time - the reservation's row lock (specs/001) and the saga's compensation (specs/002)
  holding across five services at once. The integration tests prove the lock inside Inventory; this proves it
  through the gateway, the cart, Order, the broker and the saga together.
- **Settle time under contention is queueing, not work.** One stock row takes one lock at a time, so 100
  reservations of it are served one after another; the slowest order in a race waits for the 99 before it.
- **One run proves little about speed on this machine.** A breakdown by stage, from each service's own
  timestamps, shows the same code with a 0.3 s median in one run and 10 s in the next; the time goes to
  Inventory's reservation and confirmation, waiting on the one stock row every checkout of one product shares,
  whose lock hold time grows whenever the laptop is busy. Runs right after a rebuild are slower still (a cold
  start). Judge over several warm runs - which is why checkout has several above.
- **Latencies are not thresholds.** A run fails on an unexpected response or a broken invariant, never on speed
  (specs/144 research D5): on a laptop running two dozen containers, a latency limit would fail for reasons
  that have nothing to do with the code.

## Runs

| Summary | Scenario | Passed |
| :-- | :-- | :-- |
| [`browse-2026-10-03T12-13-27-976Z.json`](../../server/loadtest/results/browse-2026-10-03T12-13-27-976Z.json) | browse | yes |
| [`checkout-2026-10-03T12-08-41-527Z.json`](../../server/loadtest/results/checkout-2026-10-03T12-08-41-527Z.json) | checkout | yes |
| [`checkout-2026-10-03T12-10-13-439Z.json`](../../server/loadtest/results/checkout-2026-10-03T12-10-13-439Z.json) | checkout | yes |
| [`checkout-2026-10-03T12-11-40-364Z.json`](../../server/loadtest/results/checkout-2026-10-03T12-11-40-364Z.json) | checkout | yes |
| [`race-2026-10-03T12-05-36-922Z.json`](../../server/loadtest/results/race-2026-10-03T12-05-36-922Z.json) | race | yes |
| [`race-2026-10-03T12-06-28-279Z.json`](../../server/loadtest/results/race-2026-10-03T12-06-28-279Z.json) | race | yes |
| [`race-2026-10-03T12-07-13-041Z.json`](../../server/loadtest/results/race-2026-10-03T12-07-13-041Z.json) | race | yes |

Rerun: `server/loadtest/run.sh race|checkout|browse`, then `python server/loadtest/report.py`.
