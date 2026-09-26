---
description: "Task list for Test runs clean up after themselves"
---

# Tasks: Test runs clean up after themselves

> Completed on 2026-09-27, after the feature merged (#157), from the code at that merge, the pull request and
> docs/testing/testing-strategy.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/README.md](contracts/README.md)

## Format: `[ID] [P?] [Story] Description`

## As first recorded

- [X] T001 `bruno/cleanup/`: the folder, run last, and three deletes, each asserted 204 or 404.
- [X] T002 A `trap cleanup EXIT` in `.github/scripts/verify-saga.sh` and `verify-auth.sh`. Deletes are reported and never fatal, and the exit status is kept.
- [X] T003 Evidence: the product count before and after two Bruno runs and one `verify-saga.sh` run, plus a forced failure that still cleans up and exits non-zero. Docs: CLAUDE.md, the testing strategy, the timeline and the backlog.

> **Correction (backfill)**: the folder is `bruno/teardown/`, not `bruno/cleanup/`; and in `verify-auth.sh` the
> function is `auth_cleanup` (`trap auth_cleanup EXIT`).

## By file (added in the backfill; all done in #157)

- [X] T004 [US1] `bruno/teardown/folder.yml` (`seq: 17`, `auth: inherit`)
- [X] T005 [P] [US1] `bruno/teardown/delete the product this run listed.yml`, `delete the seller's product.yml`, `delete the category this run made.yml` - `adminToken`, each `expect([204, 404])`
- [X] T006 [US1] `bruno/seller/folder.yml` from `meta:` to `info:`, `seq: 16`, with a note on why - found when a teardown that ran before `seller` broke 19 requests
- [X] T007 [US2] `cleanup` and `trap cleanup EXIT` in `.github/scripts/verify-saga.sh` (product only when `PRODUCT_ID` is set; the category always)
- [X] T008 [US2] `auth_cleanup` and `trap auth_cleanup EXIT` in `.github/scripts/verify-auth.sh`
- [X] T009 Evidence: two Bruno runs 232/232 and 379/379 each, Catalog at 48 products and 19 categories throughout; `verify-saga.sh` and `verify-auth.sh` pass with the count unchanged; `SAGA_E2E_SCENARIO=reject` exits 1 and still deletes
- [X] T010 [P] Docs: CLAUDE.md seed section (runs clean up; the folder order), `docs/testing/testing-strategy.md`, Bruno counts, timeline, backlog; the memory note about the seller folder's order updated
- [X] T011 Merged as #157 on 2026-09-26 (`32178ec`), closing #118

## Dependencies

T006 before T004-T005 would run in the right place; T007-T008 independent of Bruno; evidence and docs last.
