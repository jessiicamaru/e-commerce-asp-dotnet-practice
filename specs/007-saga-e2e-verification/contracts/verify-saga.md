# Contract: `verify-saga.sh`

**Feature**: [spec.md](../spec.md) | **Date**: 2026-09-21

What the check promises, so that a change to it can be judged against something. Modelled on
`verify-auth.sh`, which is the only other thing in this repository shaped like it.

---

## Invocation

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

Needs `bash`, `curl` and `python3` (or `python`). **No `jq`** — the existing scripts avoid it
because it is not present on every machine that runs them, and the same applies here.

### Inputs

| Variable | Required | Default | Meaning |
| :-- | :-- | :-- | :-- |
| `ADMIN_EMAIL` | yes | — | the seeded administrator; creates the product and sets stock |
| `ADMIN_PASSWORD` | yes | — | as above |
| `IDENTITY_URL` | no | `http://localhost:5056` | |
| `CATALOG_URL` | no | `http://localhost:5057` | |
| `ORDER_URL` | no | `http://localhost:5059` | |
| `INVENTORY_URL` | no | `http://localhost:5060` | |
| `PAYMENT_URL` | no | `http://localhost:5061` | probed for reachability and its configured outcome |
| `SAGA_TIMEOUT_SECONDS` | no | `60` | budget for one order to reach a terminal state |
| `SAGA_E2E_REQUIRE_ALL` | no | unset | when `1`, a skipped scenario is a failure (**CI sets this**) |
| `SAGA_E2E_SCENARIO` | no | unset | `approve` or `reject` to run one scenario only; unset runs the one matching Payment's reported outcome |

`ADMIN_*` are read from the environment and never printed. The scripts in this repository already
build request bodies through a helper rather than interpolating into JSON, and this one does the
same — a password containing a quote must not produce a malformed request or, worse, a logged one.

### Exit codes

| Code | Meaning |
| :-- | :-- |
| `0` | every scenario it attempted passed, and any it skipped was permitted to skip |
| `1` | an assertion failed, an order stalled, or a scenario was skipped while `SAGA_E2E_REQUIRE_ALL=1` |
| `2` | it could not start at all — a required variable is missing, or no Python is available |

A failure emits `::error::` so GitHub annotates it, matching `verify-auth.sh`.

---

## Which scenario runs

Payment decides its outcome once at startup, so **one running Payment can only produce one of the
two scenarios** ([research D3](../research.md)). The script does not try to change that; it reads
what Payment reports and runs the matching scenario.

`GET /health` on Payment reports `provider` and `configuredOutcome`. The script uses
`configuredOutcome` to choose, which means:

> **The two vocabularies do not match, and it is easy to lose an hour to this.** `PAYMENT_OUTCOME`
> is set to `Approve` or `Reject` (`PaymentOutcomeOptions.ApproveValue` / `RejectValue`), while
> `configuredOutcome` reports the *resulting* `PaymentStatus` — `Approved` or `Rejected`. Input is
> the verb, output is the past participle. A script matching the health field against `"Approve"`
> finds nothing and silently takes the wrong branch, so the comparison must be written against
> `Approved` / `Rejected`, or prefix-matched deliberately.


- It never asserts "completed" against a Payment that is going to refuse, and vice versa — a
  mismatch would be a confusing failure about stock when the real answer is that the wrong service
  is running.
- **Both scenarios in one run means invoking it twice**, with Payment restarted in between. The CI
  job does exactly that; the contract does not hide it behind a flag that cannot work.

`SAGA_E2E_SCENARIO` overrides the choice. It exists for the negative control: forcing `approve`
against a refusing Payment is control 1 ([research D6](../research.md)), and it must fail.

---

## Output

One line per step, in the shape the auth script already uses:

```text
Identity:  http://localhost:5056
Catalog:   http://localhost:5057
Order:     http://localhost:5059
Inventory: http://localhost:5060
Payment:   http://localhost:5061  (provider=Stub - no money is moved, outcome=Approved)

  ok  administrator signed in
  ok  product created: 01a0b1c2-...  (stock row registered after 1.2s)
  ok  stock set to 50 on hand
  ok  customer registered and signed in
  ok  stock before: on-hand=50 reserved=0 available=50
  ok  order 01a0b1c3-... submitted
  ok  order reached Completed after 2.8s
  ok  on-hand fell by exactly 3 (50 -> 47)
  ok  nothing left held (reserved 0 -> 0)
  ok  available fell by exactly 3 (50 -> 47)

1 of 1 scenario exercised: approve=pass
```

The closing count is not decoration. It is the line that distinguishes *"ran everything and found
nothing wrong"* from *"ran nothing"*, and this repository has already shipped a check where those
were indistinguishable.

### Skips

A skipped scenario says so on its own line and again in the count:

```text
  --  SKIPPED: Inventory is not listening on http://localhost:5060

0 of 1 scenario exercised: approve=SKIPPED
```

Exit `0` normally; exit `1` when `SAGA_E2E_REQUIRE_ALL=1`.

### Failures

A failure names the guarantee, not just the values:

```text
::error::Stock still held after the order completed: reserved went 0 -> 3, expected 0 -> 0.
         The order settled but Inventory never confirmed the reservation. Check whether two
         services declare a consumer class of the same name - they would share one queue.
```

A stall is reported as a stall, never as a wrong outcome:

```text
::error::Order 01a0b1c3-... was still 'Submitted' after 60s. It never settled, which is not
         the same as settling wrongly - look for a service that is down or a queue with the
         wrong number of consumers, not at the order's data.
```

---

## What it promises

1. It places a real order over HTTP with a real signed customer token. It never writes to any
   service's database, and it never reads one. Principle I applies to the check as much as to the
   services — a check that breaks the rule it verifies is not evidence.
2. Every quantity assertion is against a reading taken in the same run, immediately before the
   order. It compares against no constant.
3. It asserts on a terminal order status only. Nothing is read between submission and settlement and
   treated as a result.
4. It asserts on `QuantityOnHand` **and** `QuantityReserved`, not only on `QuantityAvailable`. The
   bug that motivated the feature left `Available` correct
   ([data-model](../data-model.md#observable-state-stock-for-one-product)).
5. It creates its own product each run, so nothing it observes depends on what an earlier run left.
6. It distinguishes, in its exit code and its message, between an order that settled wrongly and one
   that never settled.

## What it does not promise

- **That the flow works under load.** One order at a time. Concurrency is covered per-service,
  against a real database.
- **That the gateway routes correctly.** It talks to services directly. A gateway variant is a later
  and smaller piece of work.
- **That payment moves money.** It does not; `Provider` is `Stub` on every row and the health
  endpoint says so. The check reads that field and would notice if it ever stopped saying it.
- **That every ordering of events is safe.** It exercises the two orderings that occur; it is not a
  model checker.
- **That the expiry sweeper returns stranded stock.** It asserts that compensation returns it
  *promptly*, which is the guarantee. The sweeper is a backstop, and a check that waited for it
  would pass on a broken compensation path.
