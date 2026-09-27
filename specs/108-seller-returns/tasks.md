---
description: "Task list for A seller's list of the returns of their parcels"
---

# Tasks: A seller's list of the returns of their parcels

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Order

- [ ] T001 [US1] `GetSaleReturnsQuery` + validator; `GetPageAsync` seller filter; the `sales/returns` route
- [ ] T002 [US2] `SaleSummaryResponse.ReturnStatus` and its subquery
- [ ] T003 Tests in `ReturnTests`: own return listed; another seller's and the shop's never; status filter; badge

## Phase 2: Storefront

- [ ] T004 [US1] `/shop/returns` (tabs, rows to the sale), route and menu entry; words en/vi; tests
- [ ] T005 [US2] The badge on `/shop/sales`; test

## Phase 3: Verification and docs

- [ ] T006 Mutations (quickstart Scenario 3), each red; full Order and client suites
- [ ] T007 [P] Bruno; rebuilt Order and storefront; post-design Constitution re-check
- [ ] T008 Docs: returns, marketplace, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T009 Merged as #228, closing #215
