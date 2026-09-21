# Tasks: The Checkout Flow Is Verified End to End

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `007-saga-e2e-verification`

**Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**No unit tests here.** The deliverable *is* a test, and a test cannot be its own evidence. What
stands in for unit tests is the pair of **negative controls** in Phase 5 — proving the check goes red
against a broken flow, and stays quiet only when it is allowed to. They are tasks, not intentions:
this repository shipped a check that could not fail and believed it for five days.

**No `server/` changes.** FR-014 forbids adding a seam so the system can be steered by its own test.
If a task appears to need one, that is a finding to record, not a change to make.

**Organization**: by user story.

---

## Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

`[P]` = parallelizable (different file, no incomplete dependency).

---

## Phase 1: Setup

- [ ] T001 Record the current pipeline duration from the most recent run of `.github/workflows/ci.yml` on `main`, so SC-008 has a baseline to compare against rather than an opinion. Note it in [quickstart.md](./quickstart.md) under "Measure, do not assume"
- [ ] T002 [P] Bring the full stack up locally — `cd server && docker compose up -d && ./start-dev.sh` — and confirm all six services answer `/health`. Nothing in this feature can be developed against a partial stack, and finding that out at the assertion step wastes an hour
- [ ] T003 [P] Confirm `Get-Process | Where-Object { $_.ProcessName -like 'Ecommerce.*' }` reflects what you think is running. A leftover `dotnet run` from an earlier session answering on a port has already cost this repository an afternoon, and this feature restarts Payment deliberately — which is exactly the situation that produces a stray process

---

## Phase 2: Foundational

**Blocking.** Every scenario depends on these, and getting them wrong produces failures that read
like saga bugs.

- [ ] T004 Create `.github/scripts/verify-saga.sh` with the skeleton copied from `verify-auth.sh`: `set -euo pipefail`, the **probed Python interpreter** (`python3` is the Microsoft Store stub on Git Bash and prints a notice instead of running), and `fail()` / `pass()` / `json_field()` / `json_object()` / `status()`. Do not reinvent these — a reader who knows one script should know the other
- [ ] T005 `git update-index --chmod=+x .github/scripts/verify-saga.sh`. **`chmod` alone will not do it**: this repository has `core.filemode=false`, and the last two scripts added here failed CI with `Permission denied` (exit 126) for exactly this reason
- [ ] T006 Implement service discovery and reporting: read the five URLs from the environment with the documented defaults ([contract](./contracts/verify-saga.md)), probe each, and print the banner including Payment's `provider` and `configuredOutcome`. A service that does not answer is recorded as unreachable here, not discovered mid-assertion
- [ ] T007 Implement scenario selection from Payment's `configuredOutcome`, with `SAGA_E2E_SCENARIO` overriding it. **Match on `Approved` / `Rejected`, not `Approve` / `Reject`** — the input is the verb and the health field is the past participle, and a match against the wrong one silently takes the wrong branch (verified in [contract](./contracts/verify-saga.md))
- [ ] T008 Implement the skip path: a skipped scenario prints its own `--  SKIPPED: <reason>` line and is counted as skipped. `SAGA_E2E_REQUIRE_ALL=1` turns it into exit 1 ([research D7](./research.md)). Both behaviours now, not the fatal one later — the local path is what makes the script usable while developing the rest of it
- [ ] T009 Implement the closing coverage line, `N of M scenarios exercised: <name>=<outcome>`. Write it **before** any assertion exists, so that the first run of the script honestly reports exercising nothing. That ordering is the point of FR-011

**Checkpoint**: the script runs, reports what it found, asserts nothing, and says so.

---

## Phase 3: User Story 1 — A change that breaks checkout is caught (P1) 🎯 MVP

**Goal**: an order placed over HTTP completes, and the stock is actually gone.

**Independent test**: [quickstart](./quickstart.md) scenario 1.

- [ ] T010 [US1] Implement setup steps 1–2 in `.github/scripts/verify-saga.sh`: sign in as the administrator, create a category, create a product with a unique SKU. **A fresh product per run** is what makes every later reading trustworthy — nothing else is ordering it ([data-model](./data-model.md))
- [ ] T011 [US1] Implement the wait for stock registration. The row does **not** exist when `POST /api/products` returns — Inventory's `ProductCreatedConsumer` creates it in response to an event. Poll `GET /api/stock/{productId}` until it appears, and report how long it took. Treating this as synchronous produces a setup failure that reads exactly like a saga failure
- [ ] T012 [US1] Implement `PUT /api/stock/{productId}` as the administrator to set a known quantity on hand, then register and sign in a customer through `POST /api/auth/register` + login. The customer, not the administrator, places the order — FR-002
- [ ] T013 [US1] Implement the `before` reading: `GET /api/stock/{productId}`, capturing `QuantityOnHand`, `QuantityReserved` **and** `QuantityAvailable`. All three, taken immediately before the order (FR-008)
- [ ] T014 [US1] Implement order submission: `POST /api/orders` with the customer's token and a quantity smaller than the stock. The body carries no user id and must not — the handler reads it from the token, and a command that accepted one would be the defect described in Principle IV
- [ ] T015 [US1] Implement the settle poll: `GET /api/orders/{id}` as the owner, every second, until `Completed` or `Failed`, bounded by `SAGA_TIMEOUT_SECONDS` (default 60). Print the elapsed time on success — that number is what keeps the budget honest as the system changes ([research D4](./research.md))
- [ ] T016 [US1] Make the timeout message say it is a **stall**, quoting the last observed status, and say plainly that a stall is not the same as a wrong outcome. `Submitted` at timeout is the stall signature; the other four statuses are unreachable by design and observing one is itself a failure worth reporting ([data-model](./data-model.md))
- [ ] T017 [US1] Implement assertions A1–A4 from [data-model](./data-model.md): status `Completed`; `OnHand` down by exactly the quantity; `Reserved` unchanged; `Available` down by exactly the quantity. **A3 is the one that catches the motivating bug** — assert `Reserved` is *unchanged*, not that it is zero, so a concurrent hold on the same product cannot make the check depend on nothing else happening
- [ ] T018 [US1] Write the failure message for A3 so it names the likely cause: the order settled but Inventory never confirmed, and two services declaring a consumer class of the same name share one queue. Include the `rabbitmqctl list_queues name messages consumers` command. A check that says what to look at next is worth several that only say "failed"
- [ ] T019 [US1] Run [quickstart](./quickstart.md) scenario 1 against the local stack and paste the output

**Checkpoint**: from here, no change can silently break the successful checkout path.

---

## Phase 4: User Story 2 — The failure path puts the stock back (P1)

**Goal**: a refused payment ends the order failed and returns every held unit.

**Independent test**: [quickstart](./quickstart.md) scenario 2.

- [ ] T020 [US2] Extend the scenario dispatch so the `reject` scenario reuses the whole of US1's setup and polling, differing only in its assertions. Two copies of the setup would drift, and the drift would be invisible because only one of them runs at a time
- [ ] T021 [US2] Implement assertions B1–B4: status `Failed`; `OnHand` unchanged; `Reserved` unchanged; `Available` unchanged. **B3 is the quiet one** — stranded units come back when the expiry sweeper runs, so the symptom is "stock reappeared minutes later", which nobody reports as a bug
- [ ] T022 [US2] Restart Payment locally with `PAYMENT_OUTCOME=Reject`, run [quickstart](./quickstart.md) scenario 2, and paste the output. Note the `.env` loader now **falls back** rather than overriding, so the environment variable wins — this is the behaviour that made `PAYMENT_OUTCOME=Reject dotnet run` work from feature 005 onward
- [ ] T023 [US2] Run [quickstart](./quickstart.md) scenario 3: both scenarios, with a restart between, confirming the two runs report **different** scenario names. Two runs reporting the same one means Payment did not restart with the new setting

**Checkpoint**: the branch nobody exercises by hand is now exercised on every change.

---

## Phase 5: User Story 3 — The result can be trusted (P2)

**Goal**: the check has been seen to fail, and cannot pass by doing nothing.

**Independent test**: [quickstart](./quickstart.md) scenarios 4, 5 and 6.

**Do not defer any of these three.** They are what separates a working check from a green tick, and
they are the tasks most likely to be skipped because everything already looks fine.

- [ ] T024 [US3] **Negative control 1 — the assertions fire.** With Payment refusing, run with `SAGA_E2E_SCENARIO=approve`. Confirm exit 1 and that **both** the status assertion and the `OnHand` assertion go red. If only the status one fails, the stock assertion is inert — and the motivating bug had a *correct status* with *wrong stock*, so the check would have passed during it. Paste the output
- [ ] T025 [US3] **Negative control 2 — a stall is caught and named.** Stop Inventory, run the script, and confirm exit 1 with a message about the order never leaving `Submitted` — **not** a message about stock being wrong. Paste the output
- [ ] T026 [US3] Run [quickstart](./quickstart.md) scenario 6 in both directions: a missing service with `SAGA_E2E_REQUIRE_ALL` unset exits 0 and reports `0 of 1`; the same with it set to `1` exits 1. A check that comments on everything is noise; one that cannot report its own absence is worse
- [ ] T027 [US3] Confirm the coverage line is correct in all four cases above. Exercising zero scenarios must be readable at a glance, without opening a log (SC-007)

**Checkpoint**: the check has been falsified, so a pass from it now means something.

---

## Phase 6: CI

- [ ] T028 Add a `saga-e2e` job to `.github/workflows/ci.yml`: `needs: build`, six PostgreSQL service containers (identity 5435→5432 is published as 5432 in CI today, catalog 5433, order 5434, saga 5436, inventory 5437, payment 5438) plus RabbitMQ. Copy the health-check options from the existing job rather than inventing new ones
- [ ] T029 Apply migrations for all four services that have them — Identity, Catalog, Order and **Orchestrator** (whose DbContext lives in its WebApi project, so `--project` and `--startup-project` are the same). Inventory and Payment migrate through their own Infrastructure projects
- [ ] T030 Start all six services and probe each `/health` with a per-service failure message naming which one never answered. `000` means nothing is listening; anything else means it answered and is reporting unhealthy — that distinction already exists in `auth-smoke` and is worth keeping
- [ ] T031 Run the script twice: once with Payment approving, then stop Payment, start it with `PAYMENT_OUTCOME=Reject`, and run again. Set `SAGA_E2E_REQUIRE_ALL=1` on both. **Two instances at once would compete for one queue** and each order would be paid by whichever won — the very failure being tested ([research D3](./research.md))
- [ ] T032 Add `saga-e2e` to `publish`'s `needs`, so a merge whose saga check failed publishes nothing. Without this the job informs and does not guard, and FR-013 asks for a guard
- [ ] T033 Dump all six service logs on failure, head and tail, as `auth-smoke` already does. A retrying transport pushes startup lines far out of a plain `tail`
- [ ] T034 Open a pull request and confirm the job runs, is green, and its log shows **both** scenarios with a restart between them

---

## Phase 7: The acceptance test

- [ ] T035 **Reproduce the motivating bug and confirm it is caught.** Remove Order's `SetEndpointNameFormatter(new DefaultEndpointNameFormatter(prefix: "OrderSvc", ...))` call on a scratch branch, so its `OrderCompletedConsumer` collides with Inventory's again. Confirm the job goes **red** on assertion A3 and that `publish` does not run. Then restore it and confirm green. **This is the feature's actual acceptance test** — everything before it demonstrates the script; this demonstrates that the regression class which has already happened here is now stopped before merge
- [ ] T036 Record T035's output in this file. A control that was run and not recorded is a control the next person cannot check

---

## Phase 8: Polish & Cross-Cutting

- [ ] T037 [P] Update `CLAUDE.md`: the new check beside the existing two in the Commands section, and the new CI job. Note that it needs all six services and reports **skipped** rather than passing quietly when it does not have them
- [ ] T038 [P] Update `docs/architecture/saga-orchestration-roadmap.md` — Phase 7's "E2E Verification" third is done; Seq and the real payment provider are not. Do not mark the whole phase complete
- [ ] T039 [P] Update `docs/README.md` if it indexes the scripts or the CI jobs
- [ ] T040 Fill in the before/after durations in [quickstart.md](./quickstart.md) from real runs (SC-008). If the pipeline got noticeably slower, say so — [research D2](./research.md) names the cheaper arrangement that was rejected and why
- [ ] T041 Re-read `.github/scripts/verify-saga.sh` end to end and confirm no credential is printed. `ADMIN_PASSWORD` and both tokens pass through it; a token in a public CI log is a live credential until it expires

---

## Dependencies

```text
Phase 1 (T001-T003)
   └─> Phase 2 (T004-T009)          ← skeleton, discovery, skip path, coverage line
          └─> Phase 3 US1 (T010-T019)   ← MVP: the successful path is guarded
                 └─> Phase 4 US2 (T020-T023)   ← reuses US1's setup, so it follows
                        └─> Phase 5 US3 (T024-T027)  ← needs both scenarios to falsify
                               └─> Phase 6 CI (T028-T034)
                                      └─> Phase 7 (T035-T036)
                                             └─> Phase 8 (T037-T041)
```

- **US2 depends on US1** in code, unusually for this template: T020 reuses the setup and polling
  rather than copying it. They remain independently *testable* — either scenario runs alone.
- **US3 depends on both**, because falsifying the check requires the assertions to exist.
- **Phase 6 could start after Phase 3** if the job initially ran one scenario. Not worth it: the
  restart-between-runs shape is the part of the job most likely to be wrong, and building it once
  with both scenarios present is cheaper than building it twice.

## Parallel opportunities

| Phase | Can run together |
| :--- | :--- |
| 1 | T002, T003 |
| 2 | one file, sequential |
| 3 | one file, sequential; T019 needs everything before it |
| 5 | strictly sequential — each control needs a different broken stack |
| 8 | T037, T038, T039 |

## Implementation strategy

**MVP is Phase 1 + Phase 2 + Phase 3** — 19 tasks, and it closes the gap that motivated the feature
on its own. Worth stopping there and running quickstart scenario 1 before starting Phase 4.

Then Phase 4 (the branch that rots), Phase 5 (proof it can fail), Phase 6 (it runs without being
asked), Phase 7 (proof it catches the real thing), Phase 8 (docs).

**The three tasks most likely to be skipped, and the reason not to**: T024 and T025 because the
script will already appear to work, and T035 because it means deliberately breaking something that
is currently fine. Those three are the entire difference between this feature and a green tick.
