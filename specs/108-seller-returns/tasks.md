---
description: "Task list for A seller's list of the returns of their parcels"
---

# Tasks: A seller's list of the returns of their parcels

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Order

- [x] T001 [US1] `GetSaleReturnsQuery` + validator; `GetPageAsync` seller filter; the `sales/returns` route
- [x] T002 [US2] `SaleSummaryResponse.ReturnStatus` and its subquery
- [x] T003 Tests in `ReturnTests`: own return listed; another seller's and the shop's never; status filter; badge

## Phase 2: Storefront

- [x] T004 [US1] `/shop/returns` (tabs, rows to the sale), route and menu entry; words en/vi; tests
- [x] T005 [US2] The badge on `/shop/sales`; test

## Phase 3: Verification and docs

- [x] T006 Mutations (quickstart Scenario 3), each red; full Order and client suites
- [x] T007 [P] Bruno; rebuilt Order and storefront; post-design Constitution re-check
- [x] T008 Docs: returns, marketplace, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T009 Merged as #228, closing #215

## Evidence

- Order: `ReturnTests` 27/27 (three new), the whole suite green; client 552/552, `oxlint` and `tsc -b` clean.
- Order mutations, each red then restored: the list ignoring `SellerId`; the list taking the shop's returns too; the
  status filter dropped; the badge reading any seller's return on the order; the badge never set.
- Client mutations, each red: the page opening on another tab; a row not linking to its sale; the tab ignored; no
  badge; the sent-back reference hidden.
- Bruno against rebuilt Order, Catalog and storefront: 344/344 requests, 553/553 tests. The run's seller has no
  delivered parcel, so the list is checked for shape and for holding none of the shop's; isolation between sellers is
  the database test's (SC-001).
- Post-design Constitution re-check: see [plan.md](plan.md) - no violation found.
