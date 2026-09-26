---
description: "Task list for Admin revenue less returns"
---

# Tasks: Admin revenue less returns

> Completed on 2026-09-27, after the feature merged (#176), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Input**: Design documents from `/specs/084-admin-revenue-returns/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included, and written first: the test was red by exactly the 22,000 refund before the fix.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 revenue, US2 top products, US3 top buyers

The original three tasks are kept in full: original T001 is T001-T002, T002 is T003-T006, T003 is T007-T009.

---

## Phase 1: Test first

- [X] T001 [US1] [US2] [US3] A test in `server/tests/Ecommerce.Order.Tests/SellerInsightsTests.cs` (`The_admin_overview_leaves_a_received_return_out_the_way_the_sellers_page_does`):
  - an order with two sellers' parcels, one returned and received with its refund;
  - another order whose return is still open;
  - admin revenue, top products and top buyers, checked against the seller's page.
- [X] T002 [P] Test helpers in the same file: `ReturnAsync(..., refund:)` writes `RefundAmount`; `BuyerOfAsync` reads an order's buyer

## Phase 2: Implementation (`OrderInsights`)

- [X] T003 `ReturnedIn` gives the refund of each received return of a sold order, with the private `ReturnedParcel` shape, in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs`
- [X] T004 [US1] Revenue subtracts it by its own keys (shop day, currency) in `RevenueByDayAsync`
- [X] T005 [US3] Buyers subtract it by their own keys (buyer, currency) in `BuyersAsync`
- [X] T006 [US2] Products leave out the lines of a returned parcel in `ProductSalesAsync`

## Phase 3: Verification and docs

- [X] T007 Mutations (each subtraction removed in turn - revenue, top products, top buyers - each turned T001 red)
- [X] T008 [P] Bruno against the rebuilt Order, and the docs: `docs/features/admin-insights.md`, `docs/features/seller-insights.md`, `docs/features/returns.md`, `CLAUDE.md`, `docs/project/decisions.md` (row 66), counts in `docs/overview/project-overview.md` and `docs/testing/testing-strategy.md`, `docs/project/timeline.md` and `docs/project/backlog.md`
- [X] T009 Merged as #176 on 2026-09-26 UTC (closes #172), after Order 266/266 and Bruno 267/267 requests and 437/437 tests

---

## Dependencies & Execution Order

- T001 first, and red.
- T003 before T004 and T005, which read it; T006 is independent of T003.
- T004-T006 are one file, so sequential in practice.
