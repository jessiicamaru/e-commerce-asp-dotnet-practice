---
description: "Task list for A seller sees how their shop is doing"
---

# Tasks: A seller sees how their shop is doing

> Completed on 2026-09-27, after the feature merged (#152), from the code at that merge, the pull request and
> docs/features/seller-insights.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/http-api.md](contracts/http-api.md)

**Tests**: Included and written first; the sums are SQL over real rows, so they run against real PostgreSQL.

## Format: `[ID] [P?] [Story] Description`

## As first recorded

- [X] T001 [US1] Tests first in `server/tests/Ecommerce.Order.Tests/SellerInsightsTests.cs`. Cover:
  - own lines only;
  - a shared order;
  - sold statuses only;
  - a received return excluded;
  - orders counted once;
  - per currency;
  - the period rule;
  - top products.
- [X] T002 [US1] Order: the queries, the repository rows, the grouping shared with the admin's insights, and `SalesInsightsController`.
- [X] T003 [US2] Tests in `server/tests/Ecommerce.Catalog.Tests/SellerProductInsightsTests.cs`, then the query, the repository method and the route.
- [X] T004 [US1] [US2] Storefront:
  - the service calls, the hook, and the chart moved to `components/insights`;
  - `pages/shop-insights`, with its route, its menu entry and its words in vi and en;
  - Vitest tests.
- [X] T005 Bruno (the seller folder: 200 for the seller, and a customer 403) and the reference regenerated. Then docs: the feature page, the timeline, the backlog and the counts.

## The same work by file (added in the backfill; all done in #152)

- [X] T006 [US1] `GetSellerRevenueQuery`, `GetSellerTopProductsQuery`, their validators (`ValidPeriod`, limit 1-50) and the shared `Revenue` / `TopProducts` grouping in `server/src/Services/Order/Ecommerce.Order.Application/Insights/InsightsFeatures.cs`
- [X] T007 [US1] `SellerLinesIn`, `SellerRevenueByDayAsync`, `SellerProductSalesAsync` and the `SellerLine` class in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs`
- [X] T008 [US1] `server/src/Services/Order/Ecommerce.Order.WebApi/Controllers/SalesInsightsController.cs`
- [X] T009 [US2] `SellerProductInsight(s)` and `SellerAsync` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductViewRepository.cs` and `.../Infrastructure/Persistence/Repositories/ProductViewRepository.cs`
- [X] T010 [US2] `GetMyProductInsightsQuery` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Views/ProductViewFeatures.cs`; `GET insights/mine` in `ProductsController.cs`
- [X] T011 [P] Move the chart, revenue panel, ranked list, insight panel and period picker to `client/src/components/insights/`; words to `common` (`insights.*`); `pages/admin-overview` uses them
- [X] T012 [P] `Insights.sellerRevenue`, `sellerTopProducts`, `mine` and their types in `client/src/services/insights/`; `useSellerInsights` in `client/src/hooks/insights/index.ts`; `periodRange` in `client/src/utils/insights/index.ts`
- [X] T013 `client/src/pages/shop-insights/` (5 tests); route in `client/src/routes/index.tsx`; menu entry in `client/src/layouts/seller-layout/index.tsx`
- [X] T014 [P] Bruno `bruno/seller/` 56-59
- [X] T015 Mutation checks - 9 of 9 caught, each reverted and the file touched (table in the PR)
- [X] T016 [P] Docs: new `docs/features/seller-insights.md`, `admin-insights.md` updated, reference regenerated (127 endpoints), decision 51, timeline, backlog (#111 to Fixed), counts (server 686, storefront 378, Bruno 219), CLAUDE.md
- [X] T017 Merged as #152 on 2026-09-26 (`87e1797`), closing #111

## Dependencies

T001 before T006-T008; T003 before T009-T010; the server before the page (T011-T013); checks and docs last.

## Implementation notes

- The plan said the words would move to `common` → `chart.*`; they went to `insights.*` (see the correction in
  [plan.md](plan.md)).
- The live run had no sales for the Bruno seller, so live revenue was empty; the figures are proven by the tests.
