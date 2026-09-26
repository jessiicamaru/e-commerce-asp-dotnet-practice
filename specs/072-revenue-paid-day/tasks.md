---
description: "Task list for Revenue counts on the day an order was paid"
---

# Tasks: Revenue counts on the day an order was paid

> Completed on 2026-09-27, after the feature merged (#156), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md)

## Format: `[ID] [P?] [Story] Description`

## As first recorded

- [X] T001 Tests first. `InsightsTests` and `SellerInsightsTests`: placed on one day and paid on the next, for the admin and the seller; no `PaidAt`, so the day it was placed. `SettlementTests`: `PaidAt` is written once, and not for a failure.
- [X] T002 `Order.PaidAt`, the migration, `SettleAsync`, `OrderInsights` (one `SaleDay`), and the detail response.
- [X] T003 Mutation checks, the reference, and the docs (admin and seller insights pages, the timeline, the backlog, the counts).

> **Corrections (backfill)**: the tests went into one new file, `server/tests/Ecommerce.Order.Tests/RevenueDayTests.cs`
> (3 tests covering all three cases, admin and seller), not into `InsightsTests`, `SellerInsightsTests` and
> `SettlementTests`, which #156 did not touch. And there is no `SaleDay`: `PaidAt ?? CreatedAt` is written in three
> places, as the plan says (research D3).

## By file (added in the backfill; all done in #156)

- [X] T004 [US1] `server/tests/Ecommerce.Order.Tests/RevenueDayTests.cs` (3); one test in `VoucherCheckoutTests.cs` now moves `PaidAt` with `CreatedAt`
- [X] T005 [US1] `PaidAt` in `server/src/Services/Order/Ecommerce.Order.Domain/Entities/Order.cs`; migration `server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/20260926085159_AddOrderPaidAt.cs`
- [X] T006 [US1] `SettleAsync` sets `PaidAt` in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs`
- [X] T007 [US1] `SoldIn`, the admin's day grouping and `SellerLinesIn`'s `Day` in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs`
- [X] T008 [US2] `PaidAt` on `OrderDetailResponse` (`OrderResponses.cs`) mapped in `OrderMapping.cs`
- [X] T009 Mutation checks - 6 of 6 caught (the first run counted a build error as a catch; that mutation was rerun with valid syntax and is caught by two tests)
- [X] T010 Live: Order image rebuilt; `verify-saga.sh` `approve=pass`; `PaidAt` 0.3-1.3 s after `CreatedAt`
- [X] T011 [P] Docs: admin-insights rule 12 and the limit it replaced, seller-insights, `docs/reference/data-model.md`, counts, timeline, backlog, CLAUDE.md
- [X] T012 Merged as #156 on 2026-09-26 (`11d9349`), closing #116
