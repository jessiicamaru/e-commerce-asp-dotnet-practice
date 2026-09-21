# Phase 0 Research: The Checkout Flow Is Verified End to End

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-21

Eight decisions, each with what was chosen, why, and what was rejected. Plus three things found while
reading that the feature did not go looking for.

---

## D1 — A shell script against running services, not a test project

**Decision**: `.github/scripts/verify-saga.sh`, in the same shape as the existing
`verify-auth.sh` — bash, `curl`, `python3` for JSON, no `jq`, and the same `fail()` / `pass()` /
`json_field()` helpers.

**Rationale**: The defect this feature exists to catch **cannot be reproduced in-process**.

Inventory and Order each declared a consumer class named `OrderCompletedConsumer`. MassTransit
derives a queue name from the class name, so both bound to `order-completed` and competed for the
event instead of each receiving a copy. The order settled; the stock stayed held.

A test using `AddMassTransitTestHarness` would not have seen it. The harness dispatches in memory
and creates no queues, so two consumers with the same name are simply two consumers. A
`WebApplicationFactory` test would not have seen it either, for the same reason, unless it stood up
a real broker and two real service processes — at which point it is orchestrating external
processes from inside a test host, which is a shell's job done badly.

The property under test is *"six separate processes, connected to one real broker, agree"*. That
property only exists between processes. Constitution V: a check must exercise the real dependency
for the property it claims to verify.

A script also runs locally against `./start-dev.ps1` unchanged, which is where a developer would
actually reach for it.

**Alternatives considered**:

- **An xUnit project (`Ecommerce.SagaE2E.Tests`) with the MassTransit test harness.** Would reuse the
  existing test conventions and run under `dotnet test` with everything else. It would also be
  blind to the exact bug that motivates the feature, which makes it the wrong tool however tidy it
  looks.
- **Testcontainers, starting six services as containers from inside a test.** Genuinely does exercise
  real queues. It duplicates what `docker-compose.app.yml` already expresses, adds a dependency, and
  builds seven images to run one order. Reconsider if the check ever needs to run somewhere without
  a pipeline.
- **Extending `verify-auth.sh`.** One script, two unrelated jobs. The auth script's value is that it
  is short enough to read in full.

---

## D2 — Its own CI job, parallel to the auth smoke test

**Decision**: a new `saga-e2e` job, `needs: build`, alongside `auth-smoke` rather than inside it.
`publish` gains it as a dependency: `needs: [build, auth-smoke, saga-e2e]`.

**Rationale**: `auth-smoke` starts three services. The saga needs six. Merging them means a Payment
startup failure turns the **authentication** check red, and this repository has already spent hours
on failures that were reported somewhere other than where they happened — a stray host process on
5061, a build failure masking a publish job that had never run.

The two jobs both depend only on `build`, so they run concurrently and the wall-clock cost is
`max()`, not `sum()`.

**The cost is honest and worth stating**: a second job re-checks out and re-builds the solution,
roughly 1m40s of runner time, mitigated only by the NuGet cache. That is real. It buys a failure
that names itself, and this pipeline's history says that is worth more than the minutes.

**Alternatives considered**:

- **Extend `auth-smoke` and rename it.** Cheapest in runner minutes — it already has the build, the
  migrations, and four of the six databases. Rejected for the diagnosis reason above. It is the
  right answer if runner minutes ever become the binding constraint.
- **Reuse the build output via an artifact upload.** Avoids the second build. Adds an upload, a
  download, and a new way for the two jobs to disagree about what they ran. Worth doing only if the
  duration measured in T0xx turns out to matter.

---

## D3 — Two payment outcomes means two payment lifetimes

**Decision**: run the success scenario, stop Payment, start it again with `PAYMENT_OUTCOME=Reject`,
run the failure scenario.

**Rationale**: Verified, not assumed. `StubPaymentGateway` takes `IOptions<PaymentOutcomeOptions>`
and resolves the outcome **once, in its constructor**:

```csharp
public StubPaymentGateway(IOptions<PaymentOutcomeOptions> options)
{
    var configured = options.Value.Outcome?.Trim();
    _configuredOutcome = configured switch { ... };
}
```

One running Payment produces one outcome for its whole life. Restarting it is the only way to get
the other without touching production code.

**Alternatives considered**:

- **Two Payment instances at once, configured differently.** The obvious idea, and it is
  *specifically wrong here*: both would host a `ProcessPaymentCommand` consumer of the same class
  name, bind to the same queue, and **compete** — so each order would be paid by whichever instance
  happened to win. That is precisely the failure mode this feature exists to detect, reproduced by
  the test that is supposed to detect it. Recorded because it is the first thing anyone will suggest.
- **`IOptionsMonitor` plus an endpoint to flip the outcome at runtime.** Would make the restart
  unnecessary. It also adds a way to change a payment decision from outside the process, to
  production code, for the benefit of a test — FR-014 forbids exactly this, and it would be the
  single most dangerous seam in the system.
- **Deciding the outcome from the order (e.g. reject above a magic amount).** Same objection, and it
  means the rejection path the check verifies is one that exists only for the check.

---

## D4 — Waiting for an asynchronous outcome, and saying which kind of failure happened

**Decision**: poll `GET /api/orders/{id}` as the order's owner until the status is terminal, with a
**60-second** budget and a 1-second interval. Report the elapsed time on success, and on timeout
report the **last observed status**, not merely that time ran out.

**Rationale**: Only three of the seven `OrderStatus` values are reachable — `Submitted`,
`Completed`, `Failed` (the other four are unreachable by design; see
[specs/003-order-lifecycle/data-model.md](../003-order-lifecycle/data-model.md)). That makes the two
failure kinds trivially distinguishable, which FR-009 requires:

| Last observed | Meaning |
| :-- | :-- |
| `Submitted` at timeout | the flow stalled — a service is down, a queue is misbound, a message is stuck |
| `Failed` when `Completed` expected | the flow ran and reached the wrong outcome |

60 seconds is chosen to be generous rather than tight. A shared runner under load is slow, and the
cost of a wait that is too long is paid **only when something is already broken**, whereas the cost
of one that is too short is paid on healthy runs, repeatedly, until people stop reading the result.
Printing the elapsed time on success is what keeps the number honest: if settlement normally takes
three seconds and starts taking forty, that is visible long before the budget is hit.

**Alternatives considered**:

- **A fixed sleep, then one read.** Simpler and wrong in both directions: slow when healthy, flaky
  when loaded.
- **Subscribing to the broker and waiting for `OrderCompletedEvent`.** Observes an internal message
  rather than what a customer sees, and would need its own queue — a new consumer, on the same
  broker, in the check. Adding a queue to a check whose purpose is to catch queue mistakes is not a
  trade worth making.

---

## D5 — Test data is created through the public API, including the part that is asynchronous

**Decision**: the check builds everything it needs through HTTP, in this order:

1. Admin logs in (Identity).
2. Admin creates a category and a product (Catalog).
3. **Wait** for Inventory to register a stock row for that product — Catalog publishes
   `ProductCreatedEvent` and Inventory's `ProductCreatedConsumer` creates the row, so it does not
   exist yet when the product is created.
4. Admin sets the quantity on hand (`PUT /api/stock/{productId}`).
5. A customer registers and logs in.
6. Read stock; place the order; poll; read stock again.

**Rationale**: FR-002 and FR-008. Every quantity is read, never assumed — the databases persist
between runs and earlier runs leave data behind. Step 3 is the one that is easy to miss: it is an
asynchronous gap inside the *setup*, and treating it as synchronous produces a check that fails for
a reason having nothing to do with checkout.

`GET /api/stock/{productId}` is `[AllowAnonymous]`, so the after-reading needs no token, and
`StockResponse` carries `QuantityOnHand`, `QuantityReserved` and `QuantityAvailable` — enough to
assert FR-005 (nothing still held) directly rather than by inference.

**Alternatives considered**:

- **Seeding rows straight into the databases.** Faster and forbidden: Principle I says no component
  touches another service's database, and a check that breaks the rule it is verifying compliance
  with is not evidence of anything.
- **Reusing a fixed well-known product id.** Removes the setup and couples every run to state left
  by the last one — the failure mode FR-008 exists to prevent.

---

## D6 — Negative controls: two, in different directions

**Decision**: prove the check can fail before believing that it passes.

1. **Assertions fire** — run the *success* scenario against Payment configured to **Reject**. The
   order ends `Failed` where `Completed` is expected, and the stock returns where a decrease is
   expected. **Both** assertions must go red; if only the status one does, the stock assertion is
   not doing anything.
2. **Stalls are caught** — run with Inventory **not started**. The order never leaves `Submitted`,
   and the check must fail reporting a stall, not a wrong outcome.

**Rationale**: This repository shipped `verify-image-has-no-secrets.sh` in a state where it could
not fail — it examined the wrong level of a nested archive and reported clean on an image that
provably leaked. It was believed for five days, and it was found only because somebody deliberately
planted a credential. Feature 006 made negative controls a task rather than an intention for that
reason, and this feature inherits the rule.

Control 1 is the important one, because it is the only thing that proves the **stock** assertion has
teeth — and the stock assertion is the entire point. The motivating bug produced a *correct order
status* with *wrong stock*, so a check that verified only the status would have passed during it.

**Alternatives considered**:

- **Reproducing the queue-name collision itself** as the control. The most faithful possible control,
  and it needs a deliberately broken build of two services. Worth doing once by hand; too heavy to
  keep.
- **Asserting a deliberately wrong expected number.** Proves the comparison operator works and
  nothing else.

---

## D7 — "Could not run" is a pass locally and a failure in CI

**Decision**: the script reports a skipped scenario plainly, as `verify-auth.sh` already does for the
Order checks. `SAGA_E2E_REQUIRE_ALL=1` turns any skip into a failure, and **CI sets it**.

**Rationale**: The two contexts mean different things by the same situation.

Locally, against `./start-dev.ps1` with Payment not running, skipping and saying so is correct and
useful. In CI, the job itself starts Payment — so "Payment is not listening" is not a reason to skip,
it is the failure. Without the distinction, a startup regression would show as a green tick over a
check that quietly did nothing, which is FR-010's exact prohibition.

This resolves the apparent tension between FR-010 (report, do not pass quietly) and FR-013 (a
failing check blocks): the skip is honest in both cases, and whether it is fatal depends on who
promised the service would be there.

**Alternatives considered**:

- **Always fail when anything is missing.** Makes the script useless locally, where a developer
  often has three of six services up.
- **Always skip.** Puts a hole in CI of exactly the shape this repository has already fallen through.

---

## D8 — The check states its own coverage

**Decision**: finish with a line naming each scenario and its outcome, and a count:
`2 of 2 scenarios exercised`. The count is what CI reads at a glance.

**Rationale**: FR-011, and the same lesson as `read N filesystem layer(s)` in the secret scanner —
added after a run that examined zero layers was indistinguishable from one that examined nine and
found nothing. Reporting the count makes "did nothing" visible without reading the log.

---

## Found while reading, not looked for

**`postgres-inventory` is started by `auth-smoke` and never used.** The job provisions it on port
5437 and sets `INVENTORY_DB_NAME` / `INVENTORY_DB_PORT`, but Inventory is neither migrated nor
started there, so nothing connects to it. It appears to be vestigial. This makes D2's cost slightly
lower than it looks — the saga job needs that database and the pipeline is already paying for one.

**A comment in `ci.yml` sits above the wrong block.** The note beginning *"Catalog publishes through
MassTransit, so it needs a broker to reach a healthy state"* is positioned above `postgres-inventory`
and describes `rabbitmq`, several blocks below. Harmless, and the kind of thing that sends the next
reader in a wrong direction for ten minutes.

**The constitution and the practice disagree about branches.** Development Workflow says *"Commit
directly to `main`; this project does not use feature branches."* Features 003, 004, 005 and 006
each went through a feature branch and a pull request (#3, #5, #10, #11), and the `dev-new-session`
skill creates one deliberately, explaining why. Governance is explicit that a disagreement between
the code and the constitution *"is never resolved by ignoring it"* — so either the sentence should
be amended to describe what is actually done (routine fixes direct to `main`, Spec Kit features on a
branch), or the practice should change. **Not resolved here**, because amending the constitution is
its own procedure and does not belong inside an unrelated feature. Recorded so it is not found a
fifth time.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| The 60-second budget is still too short on a loaded runner | A false failure, and people learn to re-run without reading | Elapsed time printed on every success, so the real distribution becomes visible instead of guessed at |
| Six services in one job do not all become healthy | The check fails for a reason unrelated to checkout | Startup probes per service, and a failure that names which one never answered — the existing job already does this for three |
| The check passes because it silently did nothing | The worst outcome, and the one with precedent in this repository | D6 (both controls) and D8 (scenario count), as tasks rather than intentions |
| The second job's rebuild makes the pipeline annoying | People skip checks or stop merging | Measured before and after, recorded, not assumed. D2 names the cheaper fallback if it matters |
| Data left by earlier runs changes the answer | Intermittent, and very hard to read | Every quantity is a before/after pair (D5); the check creates its own product each run |
