# Implementation Plan: Load-test checkout and measure it

**Branch**: `feat/290-load-tests` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md) | **Issue**: #290

## Summary

Three k6 scripts share a small library: staff sign-in with TOTP, catalogue setup, customer registration through
Identity, checkout and settle-polling, and the consistency check. `run.sh <scenario>` starts `grafana/k6` on the
compose network against the gateway and writes the summary to `server/loadtest/results/`. A Python script turns the
kept summaries into `docs/testing/load-test-results.md`.

## Technical Context

**Language/Version**: JavaScript (k6), bash, Python 3 for the report
**Primary Dependencies**: `grafana/k6` image; `k6/crypto` for TOTP
**Storage**: none new; summaries as JSON in the repository
**Testing**: the scripts' own thresholds and consistency checks; a negative control (the race with the stock lock
removed would oversell, but that is a code change; instead the check is shown to fail on a deliberately wrong expected
stock)
**Target Platform**: the compose stack on the developer's machine
**Project Type**: test tooling + documentation
**Performance Goals**: measured, not set
**Constraints**: no rate limit lifted or test bypass added to the services
**Scale/Scope**: three scenarios, one library, one runner, one report

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md) before design; re-checked after.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Everything goes through public APIs; no database is read across services. The consistency check asks Inventory and Order over HTTP. |
| **II. Clean Architecture Layering** | **Not applicable.** No service code changes. |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, and under test.** The race is the whole-system test of the reservation's row lock and the saga's compensation under concurrency. |
| **IV. Identity Comes From the Token** | **Pass.** Every customer acts with their own token; nothing passes a user id. |
| **V. Evidence Over Assumption** | **Planned.** Thresholds fail the run on errors; the consistency check fails it on a broken invariant; the report is generated from kept summaries; the check is shown to fail when its expectation is wrong. |

**Post-design re-check** (after implementation): see `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
specs/144-load-tests/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/loadtest/
├── run.sh                 one command: run.sh browse|checkout|race
├── lib/shop.js            sign-in (TOTP), setup, customers, checkout, settle, consistency, cleanup
├── browse.js, checkout.js, race.js
├── report.py              summaries -> docs/testing/load-test-results.md
└── results/<scenario>-<timestamp>.json
docs/testing/load-test-results.md
```

## Complexity Tracking

No violation to justify.
