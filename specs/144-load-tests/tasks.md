---
description: "Task list for Load-test checkout and measure it"
---

# Tasks: Load-test checkout and measure it

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the scenarios are the tests.

## Phase 1: The library and the runner

- [x] T001 `server/loadtest/lib/shop.js`:
  - TOTP;
  - staff sign-in with the back office's `Origin`;
  - catalogue setup and cleanup;
  - customers through Identity;
  - checkout;
  - settle polling;
  - the consistency check.
- [x] T002 `server/loadtest/run.sh`: `grafana/k6` on the compose network, the machine named, the summary kept

## Phase 2: Scenarios (US1-US3)

- [x] T003 `race.js`: 100 customers, 20 units, shared iterations; invariants
- [x] T004 `checkout.js`: a constant arrival rate of checkouts; settle time; invariants
- [x] T005 `browse.js`: ramping anonymous reads
- [x] T006 The negative control: an expectation one unit off fails the run

## Phase 3: Runs and report

- [x] T007 Race three times, checkout and browse once each; summaries kept
- [x] T008 `report.py` and `docs/testing/load-test-results.md`; testing-strategy, docs index, CLAUDE.md, timeline, backlog
- [x] T009 Merged, closes #290 - #303

## Evidence

- Final runs on main's code (#300 and #302 in), on a rebuilt stack after one discarded warm-up. Summaries are in
  `server/loadtest/results/`, the report is `docs/testing/load-test-results.md`.
  - **race** ×3: each `placed 100, paid 20, failed 80, stuck 0, on hand 0, held 0`. Exit 0.
  - **checkout** ×3: 300 / 301 / 301 checkouts, all paid. Units deducted equal paid every time; 0 held. Settle p50
    1.08 / 2.25 / 1.06 s, p95 2.97 / 9.0 / 1.58 s. Exit 0.
  - **browse**: 11,104 requests in 102 s (109 a second), 0% unexpected. Exit 0.
  - Across the seven runs: zero `40001` in Inventory's log, and every `_error` queue empty.
- Negative control: `run.sh race -e EXPECTED_STOCK=21` → `FAIL units sold equal units deducted`,
  `thresholds on metrics 'broken_invariants' have been crossed`, exit 99.
- **What the scenarios found** on their first runs, each filed, fixed and merged before these final runs:
  - **#299** (specs/145, #300): a paid order's `OrderCompletedEvent` in Inventory's error queue. The fault was `40001`
    with no retry: 301 paid, 300 deducted, 1 held for the expiry sweeper to resell. Fixed by a transient retry on every
    consumer, before the outbox.
  - **#301** (specs/146, #302): 1,710–5,610 `40001` aborts per run on one product's stock row at `REPEATABLE READ`.
    Inventory now consumes at `READ COMMITTED`: zero aborts.
- A lesson recorded in CLAUDE.md, the report and the records: latency on this laptop swings between a 0.3 s and a 10 s
  median for identical code, and the slowest stages are Inventory's waits on the hot row. A run right after a rebuild
  is a cold start. Speed is judged over several warm runs; the scenarios fail only on invariants and errors.
