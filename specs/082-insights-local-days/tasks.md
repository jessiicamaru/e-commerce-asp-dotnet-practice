---
description: "Task list for Insights in the shop's days"
---

# Tasks: Insights in the shop's days

> Completed on 2026-09-27, after the feature merged (#170), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Input**: Design documents from `/specs/082-insights-local-days/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. Which day an instant counts on is a property of the database's `AT TIME ZONE`, so it is tested
against PostgreSQL (constitution V).

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 a morning sale on its own morning, US2 the chart draws the server's days, US3 views and a seller's
  figures on the same days

The original five tasks are kept in full: original T001 is T001, T002 is T002-T004, T003 is T005-T006, T004 is
T007-T008, T005 is T009-T011.

---

## Phase 1: Foundational (the shared calendar)

- [X] T001 `InsightsCalendar` in `server/src/BuildingBlocks/Ecommerce.Shared/Insights/InsightsCalendar.cs` (`DayOf`, `StartOf`, `ZoneId`) and `AddInsightsCalendar` reading `Insights:TimeZone`, refusing an unknown zone at startup; `InsightsPeriod.Resolve` and `ValidPeriod` take the calendar in `.../Insights/InsightsPeriod.cs`.

## Phase 2: User Story 1 - a morning sale counts on its own morning (P1)

- [X] T002 [US1] Test first in `server/tests/Ecommerce.Order.Tests/InsightsTests.cs`: an order paid at 23:30 UTC counts on the next day in Hanoi (`An_order_paid_late_at_night_UTC_counts_on_the_next_morning_in_Hanoi`); the existing insight tests use the shop's days (`Day()` returns a Hanoi midnight), and `SellerInsightsTests.cs` too
- [X] T003 [US1] Order: `OrderInsights` groups by the shop's date (`AT TIME ZONE`) in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs`; the handlers and all five validators take the calendar in `.../Order.Application/Insights/InsightsFeatures.cs`, and `RevenueResponse` carries `FirstDay`/`LastDay`
- [X] T004 [US1] Register `AddInsightsCalendar` in `server/src/Services/Order/Ecommerce.Order.WebApi/Program.cs` and in `server/tests/Ecommerce.Order.Tests/OrderTestFixture.cs`

## Phase 3: User Story 3 - views and a seller's figures (P3)

- [X] T005 [US3] Catalog: a view is recorded on the shop's today; top viewed and a seller's own insights resolve their period with the calendar, in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Views/ProductViewFeatures.cs`
- [X] T006 [P] [US3] Register `AddInsightsCalendar` in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs` and `server/tests/Ecommerce.Catalog.Tests/CatalogTestFixture.cs`; adjust `ProductViewTests.cs`

## Phase 4: User Story 2 - the chart draws the server's days (P2)

- [X] T007 [US2] Storefront: `periodDays(first, last)` from the response's `firstDay`/`lastDay`, falling back to the dates asked for, in `client/src/utils/insights/index.ts`; `firstDay?`/`lastDay?` in `client/src/services/insights/types.ts`; both callers in `client/src/pages/admin-overview/index.tsx` and `client/src/pages/shop-insights/index.tsx`
- [X] T008 [US2] Tests for the days drawn, with dates that are not today's, in `client/src/utils/insights/index.test.ts` and `client/src/pages/admin-overview/index.test.tsx`

## Phase 5: Polish and verification

- [X] T009 Mutations: grouping by the UTC date in `OrderInsights` (fails the new test and one existing one); the Overview ignoring `firstDay`/`lastDay` (fails the chart test, once its mocked days were moved away from today)
- [X] T010 [P] Bruno against the rebuilt order, catalog and storefront: `bruno/admin-insights/revenue per currency.yml` and `bruno/seller/a seller sees their own revenue.yml` check the Hanoi midnight and the named days; and the docs - `docs/features/admin-insights.md`, `docs/features/seller-insights.md`, `CLAUDE.md`, the counts in `docs/overview/project-overview.md` and `docs/testing/testing-strategy.md`, `docs/project/timeline.md`, `docs/project/backlog.md`
- [X] T011 Merged as #170 on 2026-09-26 (closes #168), after Order 264/264, Catalog 205/205, client 456/456 with lint and type-check, and Bruno 267/267 requests and 435/435 tests

---

## Dependencies & Execution Order

- T001 blocks everything: every validator and handler takes the calendar.
- US1 (T002-T004) and US3 (T005-T006) touch different services and could run in parallel.
- US2 (T007-T008) needs `firstDay`/`lastDay` from T003 to be meaningful, but falls back without them.
- T009-T011 after all stories.

## Notes

- 11 tasks. The story phases are ordered by service, which is how the change was made; US3 is listed before US2
  for that reason, not because of priority.
