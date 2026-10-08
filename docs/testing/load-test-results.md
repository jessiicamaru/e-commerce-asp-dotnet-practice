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

Anonymous shoppers ramp to **50 at once** (30s up, 60s hold, 10s down), each listing a page, searching and opening a product, then reading for a second ([`browse.js`](../../server/loadtest/browse.js)).

Since specs/157 (#361) Catalog answers these anonymous reads from memory, emptied after every committed catalogue
write. Every run, with whether the cache was on; milliseconds, median / p95:

| Run | Catalogue cache | Requests | Unexpected | list products | search | one product |
| :-- | :-- | --: | --: | --: | --: | --: |
| 2026-10-03 12:13 UTC | off | 11,104 | 0.00% | 27.7 / 101 | 26.0 / 69.3 | 15.2 / 40.5 |
| 2026-10-08 08:19 UTC | off | 11,281 | 0.00% | 20.1 / 78.1 | 20.4 / 58.8 | 12.7 / 36.8 |
| 2026-10-08 08:21 UTC | off | 11,623 | 0.00% | 10.6 / 38.2 | 10.4 / 35.4 | 7.8 / 22.1 |
| 2026-10-08 08:23 UTC | off | 11,437 | 0.00% | 13.4 / 62.6 | 13.7 / 50.9 | 9.8 / 31.6 |
| 2026-10-08 08:25 UTC | off | 11,611 | 0.00% | 10.1 / 40.5 | 10.1 / 37.9 | 7.6 / 30.4 |
| 2026-10-08 08:29 UTC | on (warm-up after rebuild) | 11,971 | 0.00% | 2.5 / 5.2 | 2.3 / 5.0 | 2.3 / 4.9 |
| 2026-10-08 08:31 UTC | on | 11,974 | 0.00% | 2.4 / 4.8 | 2.3 / 4.5 | 2.2 / 4.6 |
| 2026-10-08 08:33 UTC | on | 11,941 | 0.00% | 3.0 / 6.0 | 2.9 / 5.9 | 2.8 / 6.0 |
| 2026-10-08 08:34 UTC | on | 11,965 | 0.00% | 2.6 / 5.3 | 2.5 / 5.2 | 2.5 / 5.3 |
| 2026-10-08 08:36 UTC | on | 11,977 | 0.00% | 2.5 / 4.7 | 2.4 / 4.7 | 2.3 / 4.7 |

Warm runs of 2026-10-08 on the same stack and catalogue (41 products): without the cache (4 runs) list products median 10.1-20.1, p95 38.2-78.1; search median 10.1-20.4, p95 35.4-58.8; one product median 7.6-12.7, p95 22.1-36.8.
With it (4 runs) list products median 2.4-3.0, p95 4.7-6.0; search median 2.3-2.9, p95 4.5-5.9; one product median 2.2-2.8, p95 4.6-6.0. The request rate is set by the scenario's reading pauses, so latency is the measure.

Last run in detail:

| Step | Median | p95 | p99 | Slowest |
| :-- | --: | --: | --: | --: |
| list products | 2.5 | 4.7 | 7.5 | 17.9 |
| search | 2.4 | 4.7 | 7.7 | 31.5 |
| one product | 2.3 | 4.7 | 8.7 | 19.1 |

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
| [`browse-2026-10-08T08-19-35-898Z.json`](../../server/loadtest/results/browse-2026-10-08T08-19-35-898Z.json) | browse | yes |
| [`browse-2026-10-08T08-21-29-494Z.json`](../../server/loadtest/results/browse-2026-10-08T08-21-29-494Z.json) | browse | yes |
| [`browse-2026-10-08T08-23-23-629Z.json`](../../server/loadtest/results/browse-2026-10-08T08-23-23-629Z.json) | browse | yes |
| [`browse-2026-10-08T08-25-17-627Z.json`](../../server/loadtest/results/browse-2026-10-08T08-25-17-627Z.json) | browse | yes |
| [`browse-2026-10-08T08-29-23-683Z.json`](../../server/loadtest/results/browse-2026-10-08T08-29-23-683Z.json) | browse | yes |
| [`browse-2026-10-08T08-31-09-917Z.json`](../../server/loadtest/results/browse-2026-10-08T08-31-09-917Z.json) | browse | yes |
| [`browse-2026-10-08T08-33-03-651Z.json`](../../server/loadtest/results/browse-2026-10-08T08-33-03-651Z.json) | browse | yes |
| [`browse-2026-10-08T08-34-58-291Z.json`](../../server/loadtest/results/browse-2026-10-08T08-34-58-291Z.json) | browse | yes |
| [`browse-2026-10-08T08-36-52-914Z.json`](../../server/loadtest/results/browse-2026-10-08T08-36-52-914Z.json) | browse | yes |
| [`checkout-2026-10-03T12-08-41-527Z.json`](../../server/loadtest/results/checkout-2026-10-03T12-08-41-527Z.json) | checkout | yes |
| [`checkout-2026-10-03T12-10-13-439Z.json`](../../server/loadtest/results/checkout-2026-10-03T12-10-13-439Z.json) | checkout | yes |
| [`checkout-2026-10-03T12-11-40-364Z.json`](../../server/loadtest/results/checkout-2026-10-03T12-11-40-364Z.json) | checkout | yes |
| [`race-2026-10-03T12-05-36-922Z.json`](../../server/loadtest/results/race-2026-10-03T12-05-36-922Z.json) | race | yes |
| [`race-2026-10-03T12-06-28-279Z.json`](../../server/loadtest/results/race-2026-10-03T12-06-28-279Z.json) | race | yes |
| [`race-2026-10-03T12-07-13-041Z.json`](../../server/loadtest/results/race-2026-10-03T12-07-13-041Z.json) | race | yes |

Rerun: `server/loadtest/run.sh race|checkout|browse`, then `python server/loadtest/report.py`.
