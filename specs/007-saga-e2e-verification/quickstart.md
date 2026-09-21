# Quickstart & Validation: The Checkout Flow Is Verified End to End

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-21

Seven scenarios. **Scenarios 4 and 5 are the negative controls and they are not optional** — this
repository has already shipped a check that could not fail and believed it for five days. A check
that has never been seen to go red is an absent check wearing a green tick.

---

## Prerequisites

All six services running, plus infrastructure:

```bash
cd server
docker compose up -d          # databases + RabbitMQ
./start-dev.sh                # or ./start-dev.ps1
```

`PAYMENT_OUTCOME` is read once at startup, so the scenario a running Payment can produce is fixed
until it is restarted. Check which one you have:

```bash
curl -s http://localhost:5061/health | python -c "import sys,json;d=json.load(sys.stdin);print(d['provider'], d['configuredOutcome'])"
```

---

## Scenario 1 — The successful path *(US1, FR-003/004/005, SC-004)*

With Payment approving (the default):

```bash
cd server
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expect**: the order reaches `Completed`, `QuantityOnHand` falls by exactly the amount ordered, and
`QuantityReserved` ends where it started. Closing line: `1 of 1 scenario exercised: approve=pass`.

**Failure looks like**: `Completed` with `reserved` still raised. That is the bug this whole feature
exists for — the order settled and Inventory never confirmed the reservation. Check for two services
declaring a consumer class of the same name:

```bash
docker exec e-commerce-rabbitmq rabbitmqctl list_queues name messages consumers
```

A queue with **2 consumers** that should have one subscriber per service is the symptom.

---

## Scenario 2 — The compensation path *(US2, FR-006/007, SC-005)*

Restart Payment refusing, then run again:

```bash
# stop the running Payment, then:
cd server
PAYMENT_OUTCOME=Reject dotnet run --project src/Services/Payment/Ecommerce.Payment.WebApi/ &
ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
```

**Expect**: the order reaches `Failed`, and **every** quantity is exactly where it started —
`OnHand`, `Reserved` and `Available`.

**This is the scenario nobody runs by hand**, which is why it is the one most likely to have rotted.

**Failure looks like**: `Failed` with `reserved` still raised. The units are stranded. They will come
back when the expiry sweeper runs, which makes the symptom *"stock reappeared several minutes
later"* — a thing nobody reports as a bug and nobody can reproduce on demand.

---

## Scenario 3 — Both, the way CI does it *(SC-003)*

Two invocations with a restart between them, because one running Payment produces one outcome
([research D3](./research.md)). Confirm the closing line of each run names the scenario it
exercised, and that the two differ.

**Expect**: `approve=pass` from one, `reject=pass` from the other.

**Failure looks like**: both runs reporting the same scenario. Payment did not actually restart with
the new setting — and note that the `.env` loader now *falls back* rather than overriding, so an
inherited environment variable wins over the file.

---

## Scenario 4 — NEGATIVE CONTROL: the assertions fire *(US3, FR-012, SC-006)*

**Do not skip this.** It is the only thing that proves the stock assertions have teeth.

With Payment **refusing**, force the script to run the **success** scenario:

```bash
cd server
SAGA_E2E_SCENARIO=approve ADMIN_EMAIL=... ADMIN_PASSWORD=... ../.github/scripts/verify-saga.sh
echo "exit: $?"
```

**Expect**: **exit 1**, and *both* kinds of assertion red — the status is `Failed` where `Completed`
was expected, and `OnHand` did not fall.

**Why both matter**: if only the status assertion fails, the stock assertion is not doing anything,
and the check would have passed during the motivating bug — which had a *correct status* and *wrong
stock*. Paste the output; a control that was run and not recorded is a control nobody can check.

---

## Scenario 5 — NEGATIVE CONTROL: a stall is caught, and named as a stall *(FR-009)*

Stop Inventory. Run the script.

**Expect**: exit 1, and a message saying the order was still `Submitted` when the budget ran out —
**not** a message about stock being wrong. The two failures have different causes and different
fixes, and a check that reports the wrong one sends the next person to the wrong place.

---

## Scenario 6 — Skipping is loud, and fatal only where it was promised *(FR-010, FR-011, SC-007)*

| Run | Expect |
| :-- | :-- |
| Payment not listening, `SAGA_E2E_REQUIRE_ALL` unset | `SKIPPED` on its own line, `0 of 1 scenario exercised`, **exit 0** |
| The same with `SAGA_E2E_REQUIRE_ALL=1` | the same report, **exit 1** |

The distinction is the point ([research D7](./research.md)): locally, three of six services up is a
normal afternoon. In CI the job started all six, so "not listening" is the failure, not a reason to
excuse it.

**Failure looks like**: a silent pass. A run that could not execute must never be mistakable for one
that executed and found nothing.

---

## Scenario 7 — It runs in CI, on a change, without anyone asking *(FR-001, FR-013, SC-001)*

Open a pull request. Read the run.

**Expect**: a `Saga end-to-end` job, green, its log showing both scenarios exercised with a restart
between them, and `publish` gated on it.

Then break something on purpose — delete Order's `SetEndpointNameFormatter` call, which is the line
that keeps its `OrderCompletedConsumer` from colliding with Inventory's — and confirm the job goes
**red**, and that `publish` does not run.

**That last step is the feature's actual acceptance test.** Everything else demonstrates the script;
this demonstrates that a real regression, of the exact class that has already happened here, is now
stopped before it is merged.

---

## Measure, do not assume *(SC-008)*

Record the pipeline duration before and after, from a real run of each:

| | duration |
| :-- | :-- |
| before | _fill from the last run on `main`_ |
| after | _fill_ |

The new job runs in parallel with `auth-smoke`, so the expectation is that wall clock grows by
roughly the difference between them rather than by the new job's whole length. If it grows more than
that, [research D2](./research.md) names the cheaper arrangement that was rejected — say so rather
than leaving the next person to discover it.

---

## What passing all seven does not prove

- **That checkout works under load.** One order at a time, deliberately. Concurrency is covered
  per-service against a real database, and that is a different guarantee.
- **That the gateway routes correctly.** The check talks to services directly.
- **That payment works.** Payment is a stub that moves no money and says so in three places. This
  verifies the saga around it.
- **That every failure mode is covered.** It exercises two orderings of events. A message arriving
  twice, out of order, or after a long delay is covered by the per-service idempotency tests, not
  here.
