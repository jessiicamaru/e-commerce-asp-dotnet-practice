---
description: "Task list for Load-test checkout and measure it"
---

# Tasks: Load-test checkout and measure it

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the scenarios are the tests.

## Phase 1: The library and the runner

- [ ] T001 `server/loadtest/lib/shop.js`:
  - TOTP;
  - staff sign-in with the back office's `Origin`;
  - catalogue setup and cleanup;
  - customers through Identity;
  - checkout;
  - settle polling;
  - the consistency check.
- [ ] T002 `server/loadtest/run.sh`: `grafana/k6` on the compose network, the machine named, the summary kept

## Phase 2: Scenarios (US1-US3)

- [ ] T003 `race.js`: 100 customers, 20 units, shared iterations; invariants
- [ ] T004 `checkout.js`: a constant arrival rate of checkouts; settle time; invariants
- [ ] T005 `browse.js`: ramping anonymous reads
- [ ] T006 The negative control: an expectation one unit off fails the run

## Phase 3: Runs and report

- [ ] T007 Race three times, checkout and browse once each; summaries kept
- [ ] T008 `report.py` and `docs/testing/load-test-results.md`; testing-strategy, docs index, CLAUDE.md, timeline, backlog
- [ ] T009 Merged, closes #290

## Evidence

(Filled in when the work is verified.)
