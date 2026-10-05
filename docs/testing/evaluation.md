# Evaluation

What was measured, on what, and what it showed. Each section states its result, links the run that produced it, and
says what it does not show. Read with the [testing strategy](testing-strategy.md), which explains the layers, and the
[architecture overview](../overview/architecture-overview.md), which explains what is being measured.

## 1. Correctness: the tests

Counts from CI's build job and the client suite on 2026-10-05 (the current numbers are in any CI run's log):

| Layer | What it runs against | Tests |
| :-- | :-- | --: |
| Service integration tests, 9 projects | a **real PostgreSQL** per service (plus a real S3 server for Catalog), the real MediatR pipeline, MassTransit's harness | **1,175** |
| ... of which Order | checkout pricing, vouchers, parcels, returns, payouts, cancellation | 371 |
| ... Identity | sign-in, 2FA, sessions, moderation, email, personal data | 289 |
| ... Catalog | products, variants, prices, search, reviews, questions, moderation | 268 |
| ... Inventory, Payment, Activity, ApiGateway, Cart, Orchestrator | stock and reservations; payments, refunds, VNPay; audit and notifications; the gateway's pipeline; the cart; the saga | 83, 53, 52, 22, 19, 18 |
| Client unit tests (Vitest, 133 files) | hooks, guards, forms, how a refusal is shown | **742** |
| Browser flows (Playwright) | both apps against the whole stack, nothing mocked, 3 of them paying through the VNPay simulator | **11** |
| End-to-end scripts | `verify-auth.sh` (real signed tokens across services), `verify-saga.sh` (an order through all six services, both branches, plus VNPay pay and cancel) | 2 scripts |
| Bruno collection | every public endpoint through the gateway, with negative cases (401, 403, 400, 404, 409) | the whole API |

**Why a real database.** The guarantees under test are the database's: a `FOR UPDATE` row lock that stops overselling,
a unique constraint that stops a second payment, and a guarded `UPDATE ... WHERE status = ...` that refuses to settle
an order twice. An in-memory provider would pass against code that oversells.

**Do the tests test anything? Mutation checks.** When a test was written after its code, the rule it guards was
broken on purpose (a guard removed, a status added to a filter), and the test had to fail. **114 of the 151 feature
records** list their mutations and that each was caught, for example:
- specs/148: the sampler dropping readings, and the settle window ignored;
- specs/149: every unique violation retried;
- specs/151: a location without the headers.

[Testing strategy](testing-strategy.md) has the method.

**What the tests do not cover.** Load, faults and the browser's view of security headers are not unit-testable, which
is why sections 2 to 5 exist.

## 2. The concurrency guarantee under load

100 customers checked out one unit each of a variant with 20 in stock, all at once, through the gateway, the cart,
Order, the broker and the saga ([load-test results](load-test-results.md), `server/loadtest/race.js`):

| Run | Placed | Paid | Failed | Stock after | Held after |
| :-- | --: | --: | --: | --: | --: |
| 2026-10-03 12:05 | 100 | **20** | 80 | 0 | 0 |
| 2026-10-03 12:06 | 100 | **20** | 80 | 0 | 0 |
| 2026-10-03 12:07 | 100 | **20** | 80 | 0 | 0 |

Exactly 20 sold every time, with nothing oversold and nothing left held. The integration tests prove the row lock
inside Inventory. This proves it across five services and a message broker at once.

## 3. Performance

All numbers come from **one laptop** running the whole stack and the load generator together: 12 CPUs, 7.5 GiB for
Docker, 25 containers. They show the shape, not what a server would do ([load-test results](load-test-results.md)).

| Scenario | Load | Result |
| :-- | :-- | :-- |
| Steady checkout, one product | 5 checkouts a second for 60 s | 3 warm runs: 300-301 checkouts, **all paid, units deducted = units sold, 0 unexpected responses**. Settle p50 1.06-2.25 s, p95 1.58-9.00 s |
| Request latency in that run | | place order p50 65 ms / p95 110 ms; quote 29 / 62 ms; add to cart 39 / 68 ms |
| Browsing, anonymous | ramping to 50 shoppers | **11,104 requests in 102 s (109 a second), 0 unexpected**. List p50 28 ms / p95 101 ms; search 26 / 69 ms; one product 15 / 41 ms |

**How to read the spread.** Settle time on one product is queueing: every checkout of it takes the same stock row's
lock in turn. The per-stage timestamps showed identical code with a 0.3 s median in one run and 10 s in the next, as
the laptop's load changed. The time was always in Inventory's lock wait, never in charging or settling. So speed was
judged over several warm runs, never one. No run fails on latency, only on a broken invariant or an unexpected
response.

**Throughput limit, stated.** One product's checkout rate is capped by its stock row's lock hold time.
[The SKIP LOCKED study](../concepts/shopify-inventory-skip-locked-pattern.md) is the known way past it, and it was
not adopted.

**Metrics, live.** With Prometheus and Grafana ([specs/148](../../specs/148-metrics/)), a checkout run raised
`ecommerce_orders{status="Paid"}` by exactly the run's paid count (+198), counted from committed rows.

## 4. Resilience

Steady checkouts (3 a second for 120 s) while one part of the system was taken away 15 s in and brought back
([resilience results](resilience-results.md), `server/loadtest/fault.sh`):

| Fault | Down | Orders | Customer errors | Paid | Held | Error queues | Backlog cleared after recovery |
| :-- | --: | --: | --: | --: | --: | :-- | --: |
| Payment stopped | 62 s | 361 | 0 | 361 | 0 | empty | 25 s |
| RabbitMQ stopped | 63 s | 360 | 0 | 360 | 0 | empty | 89 s |
| Orchestrator restarted | 7 s | 361 | 0 | 361 | 0 | empty | 12 s |
| Inventory hung (paused) | 31 s | 361 | 0 | 361 | 0 | empty | 20 s |

**Nothing was lost under any fault, and no customer saw an error.** Orders placed during a fault waited and then
completed:
- **Payment down**: the saga held them.
- **Broker down**: the outboxes held every message, committed with its change. Grafana showed Order's outbox reach
  616 messages and drain to 0 ([specs/148](../../specs/148-metrics/)).
- **Orchestrator down**: the saga's state is in its database.
- **Inventory hung**: its messages waited in the queue.

## 5. Security

| Check | Result | Source |
| :-- | :-- | :-- |
| CodeQL, C# and TypeScript, `security-extended` | **0 alerts** (63 and 103 queries). A planted SQL injection and DOM XSS were both flagged high, then reverted | [security scanning](security-scanning.md) |
| Known-vulnerable packages | NuGet: none. Runtime npm: none. Both gates fail CI on a planted vulnerable package | [security scanning](security-scanning.md) |
| OWASP ZAP baseline, both apps | **0 FAIL**. 63 rules pass and 1 warning is accepted (`style-src 'unsafe-inline'`). The first round's six header findings were fixed | [security scanning](security-scanning.md), [specs/151](../../specs/151-security-headers/) |
| Security headers | CSP with `script-src 'self'`, nosniff, frame denial, referrer policy, permissions policy, COOP/COEP/CORP, HSTS at the entrance. Pinned by the image check; every browser flow fails on a violation | [specs/151](../../specs/151-security-headers/) |
| Dependabot | NuGet, npm, Docker, compose, Actions, weekly | [security scanning](security-scanning.md) |

Beyond the scans, the application's own controls are each tested where they live:
- identity only from the token;
- a "not yours" that is a 404, indistinguishable from "not there";
- staff powers only in a back-office session with a second factor, checked again by every service;
- rate limits per client address that cannot be spoofed with a forwarded header;
- revoked sessions refused within seconds;
- one payment per order by a unique constraint.

The [constitution](../../.specify/memory/constitution.md) and each feature's design record say why.

## 6. Defects found by measurement

The point of measuring is to find what the tests could not. These were all found by the load, fault and metrics runs,
each filed with its evidence:

| Issue | Found by | What it was | Outcome |
| :-- | :-- | :-- | :-- |
| [#299](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/299) | checkout load test | Concurrent stock confirmations aborted with a serialization failure, and nothing retried them: 301 paid, 300 deducted, 1 held, 1 message in an error queue | **Fixed**: transient retry on every consumer ([specs/145](../../specs/145-transient-retry/)) |
| [#301](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/301) | checkout load test | 1,710-5,610 aborted-and-retried consumes per run on one popular product | **Fixed**: Inventory consumes at `READ COMMITTED`, zero aborts ([specs/146](../../specs/146-inventory-read-committed/)) |
| [#304](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/304) | broker fault | The backlog takes about 89 s to clear after a 63 s outage. The metrics and logs show the cause: each service waits for its next broker reconnect attempt, up to 30 s apart in MassTransit | **Open**, cause established |
| [#306](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/issues/306) | broker fault, while verifying metrics | A message delivered twice at once faulted on the inbox's unique key into an error queue, although its twin had paid the order | **Fixed**: that key is retried, and the inbox drops the duplicate ([specs/149](../../specs/149-inbox-redelivery/)) |

The fixes were all reproduced first, then verified by the same runs. #299 and #306 are now each covered by a test that
fails without the fix.

One claim was **corrected**. A run straight after a full rebuild was first read as the retry's cost (median settle
8.3 s against 1.0 s). It was a cold start, and the per-stage breakdown showed it. The records say so
([specs/146](../../specs/146-inventory-read-committed/)).

## 7. Limitations

- **One machine.** Every number is from a laptop running the load and the system together. Absolute latencies would
  differ on a server, and the shape would not.
- **Payment is a stub, or a simulator.** The VNPay integration is complete and tested against an independent
  simulator. No real money has moved, because there is no merchant account.
- **Not deployed to a real server yet.** The production overlay ran in full on a laptop over HTTPS, and the deploy
  script is tested with stubs ([deployment](../guides/deployment.md)).
- **No backups** of the databases or the image bucket.
- **No active security scan.** ZAP's baseline is passive by design (specs/150 research D3).
- **#304** remains open: recovery after a broker outage is slower than it needs to be, though nothing is lost.
