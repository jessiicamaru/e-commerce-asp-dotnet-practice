---
description: "Task list for admin insights"
---

# Tasks: Admin insights

> Completed on 2026-09-27, after the feature merged (#99, #101), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/](contracts/)

**Tests**: included - the view counter's guarantee belongs to the database's upsert (Principle V), and "revenue
counts sales only" can only be shown against real orders in every status.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependencies)
- **[Story]**: US1 revenue, US2 what sells / is viewed / who buys, US3 headline numbers

T001-T007 are the tasks as written during the work, kept as they were. T008 onward break them down by the files
the two merges actually touched, recorded afterwards; T008-T040 were done in #99, T041-T046 in #101.

## Original tasks

- [X] T001 Order: `IOrderInsights` (SQL grouping over sold orders), handlers per currency, `InsightsController` (Admin)
- [X] T002 Order tests: revenue per currency excludes failed, cancelled and settling; top products; top buyers
- [X] T003 Catalog: `product_views`, view command (shoppers, listed only), top viewed; migration; tests
- [X] T004 Identity: users lookup by ids, user stats; tests
- [X] T005 Bruno: admin-insights folder (200s, 403 for customer and moderator), 401 without a token
- [X] T006 Client: Overview page, product page reports a view once; tests
- [X] T007 Mutation checks (cancelled counted, staff views counted); run everything; verify-saga; docs

## Phase 1: Setup

No new project, service, database or gateway route: the three services' existing catch-all routes carry every
new path.

- [X] T008 Confirm the gateway already routes `/api/orders/{**catch-all}`, `/api/products/{**catch-all}` and `/api/users/{**catch-all}` in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json` (no change needed)

## Phase 2: Foundational

- [X] T009 [P] `ProductView` entity (`ProductId`, `Day`, `Views`) in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/ProductView.cs`
- [X] T010 [P] `ProductViewConfiguration`: table `product_views`, key (`ProductId`, `Day`), index on `Day`, cascade FK to `products` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/ProductViewConfiguration.cs`; `DbSet<ProductView>` in `.../Persistence/CatalogDbContext.cs`
- [X] T011 Migration `20260923214827_AddProductViews` (+ Designer, model snapshot) in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/`
- [X] T012 [P] `IOrderInsights` and the row records (`RevenueRow`, `ProductSalesRow`, `BuyerRow`), `InsightsPeriod` (`Resolve`, `MaxDays` = 366, `NotAfter`) in `server/src/Services/Order/Ecommerce.Order.Application/Insights/InsightsFeatures.cs`

## Phase 3: User Story 1 - Revenue (P1)

- [X] T013 [US1] Test first: `Revenue_counts_paid_orders_per_currency_and_nothing_else` - Paid, Shipped, Preparing count; Cancelled, Failed, Submitted do not; VND and USD separate; one day row per currency per day; orders moved to a far-future day of their own - in `server/tests/Ecommerce.Order.Tests/InsightsTests.cs`
- [X] T014 [US1] `OrderInsights.Sold` (Paid, Completed, Preparing, Shipped), `SoldIn(from, to)` and `RevenueByDayAsync` (group by `CreatedAt.Date`, `Currency`) in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs`; register it in `.../Infrastructure/DependencyInjection.cs` and `server/tests/Ecommerce.Order.Tests/OrderTestFixture.cs`
- [X] T015 [US1] `GetRevenueQuery`, its validator (period order, 366 days) and the revenue handler (null currency → default, totals per currency, average rounded away from zero) in `.../Application/Insights/InsightsFeatures.cs`
- [X] T016 [US1] `InsightsController` (`api/orders/insights`, `[Authorize(Roles = "Admin")]`) with `revenue` in `server/src/Services/Order/Ecommerce.Order.WebApi/Controllers/InsightsController.cs`

## Phase 4: User Story 2 - What sells, what is looked at, who buys (P1)

### Order

- [X] T017 [US2] Tests `Top_products_count_units_and_keep_revenue_per_currency` (a cancelled order's 50 units do not count) and `Top_buyers_rank_by_spend_in_the_asked_currency` in `server/tests/Ecommerce.Order.Tests/InsightsTests.cs`
- [X] T018 [US2] `ProductSalesAsync` (lines grouped by `ProductId`, `Currency`; most recent frozen name) and `BuyersAsync` (grouped by `UserId`, `Currency`) in `.../Infrastructure/Persistence/Repositories/OrderInsights.cs`
- [X] T019 [US2] `GetTopProductsQuery` (`by` units/revenue, `currency`, `limit` 1-50) and `GetTopBuyersQuery` with validators and handlers in `.../Application/Insights/InsightsFeatures.cs`; `top-products` and `top-buyers` on `InsightsController`

### Catalog

- [X] T020 [P] [US2] Tests in `server/tests/Ecommerce.Catalog.Tests/ProductViewTests.cs`: twenty at once count twenty; seller, moderator, pending product and unknown id count nothing; most viewed first. Register `IProductViewRepository` in `CatalogTestFixture.cs`
- [X] T021 [US2] `IProductViewRepository` and `ViewedProduct` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductViewRepository.cs`
- [X] T022 [US2] `ProductViewRepository.RecordAsync` (one `INSERT ... ON CONFLICT DO UPDATE SET "Views" = ... + 1`) and `TopAsync` (sum per product over the days, joined to `products`) in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductViewRepository.cs`; register it in `.../Infrastructure/DependencyInjection.cs`
- [X] T023 [US2] `RecordProductViewCommand` (listed product, not Admin, not Moderator, not its seller; quiet otherwise) and `GetTopViewedQuery` with validator and handlers in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Views/ProductViewFeatures.cs`
- [X] T024 [US2] `POST {id:guid}/view` (`[AllowAnonymous]`, always 204) and `GET insights/top-viewed` (`Admin`) on `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs`

### Identity

- [X] T025 [P] [US2] Test `Ids_are_turned_into_emails_and_unknown_ones_are_left_out` in `server/tests/Ecommerce.Identity.Tests/UserReportTests.cs`
- [X] T026 [US2] `IUserRepository.GetByIdsAsync` in `server/src/Services/Identity/Ecommerce.Identity.Application/Common/Interfaces/IUserRepository.cs` and `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs`
- [X] T027 [US2] `UserBrief`, `LookupUsersQuery` (1-100 ids) and its handler in `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`; `GET lookup` (`Admin`) on `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/UsersController.cs`

### Storefront

- [X] T028 [P] [US2] `Product.recordView` in `client/src/services/product/index.ts`
- [X] T029 [US2] One view per product opened (`useRef` guard, fire and forget) in `client/src/pages/product/index.tsx`; test "reports the page once, however often it renders" in `client/src/pages/product/index.test.tsx`

## Phase 5: User Story 3 - Headline numbers (P2)

- [X] T030 [US3] Test `The_counts_follow_the_roles` in `server/tests/Ecommerce.Identity.Tests/UserReportTests.cs`
- [X] T031 [US3] `IUserRepository.CountAsync` (per role, locked, banned, total) in `.../Application/Common/Interfaces/IUserRepository.cs` and `.../Infrastructure/Persistence/Repositories/UserRepository.cs`
- [X] T032 [US3] `UserStats`, `GetUserStatsQuery` and `UserReportHandlers` in `.../Application/Users/UserAdministration.cs`; `GET stats` (`Admin`) on `.../WebApi/Controllers/UsersController.cs`

## Phase 6: The Overview page (US1, US2, US3)

- [X] T033 [P] Types and `PERIODS` = [7, 30, 90] in `client/src/services/insights/types.ts`; the `Insights` class (revenue, topProducts, topBuyers, topViewed, userStats, people) in `client/src/services/insights/index.ts`
- [X] T034 [P] Query keys `insights(part, from)` and `people(ids)` in `client/src/constants/query-keys/index.ts`
- [X] T035 `useInsights` (six reads keyed by the period's start, emails once the buyers are known; `TOP` = 5) in `client/src/hooks/insights/index.ts`
- [X] T036 `AdminOverviewPage` (period buttons, six headline cards with the two waiting counts linking to their queues, revenue cards per currency, the daily chart, three top-5 lists) in `client/src/pages/admin-overview/index.tsx`; route `overview` in `client/src/routes/index.tsx`
- [X] T037 The Overview link, first, administrators only, in `client/src/layouts/admin-layout/index.tsx` and its test `client/src/layouts/admin-layout/index.test.tsx`; `menu.overview` and `overview.*` in `client/src/locales/en/admin.json` and `client/src/locales/vi/admin.json`
- [X] T038 Tests in `client/src/pages/admin-overview/index.test.tsx`: revenue per currency never added together; top buyers by email and the waiting count; a different period asks again

## Phase 7: Polish (#99)

- [X] T039 [P] Bruno `bruno/admin-insights/` (`folder.yml` seq 15; revenue, top selling, top buyers, who the buyers are, a shopper opens the product page, the most viewed, people in numbers - 200/204 to an administrator; a customer 403 on revenue, a moderator 403 on top-viewed) and `bruno/security-checks/insights without a token is 401.yml`
- [X] T040 Mutation checks - `Cancelled` counted as a sale, staff views counted - each red, then restored with `touch`; Order 179/179, Catalog 152/152, Identity 73/73, client 225/225, Bruno 190 requests / 307 tests, `verify-saga.sh`; `CLAUDE.md` paragraph and test counts; screenshots at 1360px and 390px

## Phase 8: The daily chart shows every day (#101)

- [X] T041 [P] [US1] `periodDays(to, count)` - every UTC day of the period, oldest first - in `client/src/utils/insights/index.ts`; tests (every day, across a month end, as many as asked) in `client/src/utils/insights/index.test.ts`
- [X] T042 [US1] `DailyRevenueChart` (one focusable column per day on a shared baseline, empty days blank, bars scaled to the peak with a 2% floor, peak named, first and last day labelled, tooltip kept inside the card, one currency) in `client/src/pages/admin-overview/daily-chart.tsx`
- [X] T043 [US1] Tests in `client/src/pages/admin-overview/daily-chart.test.tsx`: one column per day with empty days included; scaled to the busiest day; one currency at a time; the hover tooltip
- [X] T044 [US1] `RevenuePanel` hands `periodDays(to, period)` and the selected currency to the chart in `client/src/pages/admin-overview/index.tsx`; `overview.noSales` and `overview.peak` in both `client/src/locales/*/admin.json`
- [X] T045 `asyncUtilTimeout` 1s → 3s in `client/src/test/setup.ts`, after three unrelated tests failed only in the full parallel run
- [X] T046 Client 232/232 three full runs in a row, lint, type-check and build clean, screenshots at 1500px and 390px; merged as PR #99 (closes #92) and PR #101

## Dependencies & Execution Order

- Phase 2 before the Catalog work of US2 (the table) and before any Order insight (`IOrderInsights`, the period)
- US1 and US2's Order work share `OrderInsights` and `InsightsController`, so they were sequential; the Catalog
  and Identity work of US2 and US3 is independent of Order and of each other
- Phase 6 needs every endpoint; Phase 7 needs Phase 6; Phase 8 (#101) is a follow-up to a merged Phase 6

## Notes

- 46 tasks: 7 original, then 1 setup, 4 foundational, 4 for US1, 13 for US2, 3 for US3, 6 for the page, 2 polish,
  6 for #101
- Test tasks: T013, T017, T020, T025, T029, T030, T038, T041, T043 - each asserts a stated requirement rather than
  an implementation detail
