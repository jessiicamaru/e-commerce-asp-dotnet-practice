# Implementation Plan: A seller sees how their shop is doing

> Completed on 2026-09-27, after the feature merged (#152), from the code at that merge, the pull request and
> docs/features/seller-insights.md.

**Branch**: `068-seller-insights` | **Spec**: [spec.md](spec.md) | **Issue**: #111 | **PR**: #152 (merged 2026-09-26)

## Summary

Three read-only endpoints, each answered by the service that owns the data - Order for money and what sold, Catalog
for views and ratings - and one storefront page, `/shop/insights`, that composes them with the admin Overview's chart
and panels, moved to `components/insights`. No table, message or gateway route is added.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript / React 19

**Primary Dependencies**: MediatR, FluentValidation, EF Core (Npgsql); `Ecommerce.Shared.Insights.InsightsPeriod`
(specs/055); TanStack Query, react-i18next

**Storage**: none new - reads `orders`, `order_items`, `order_shipments`, `parcel_returns` (Order) and `products`,
`product_views` (Catalog)

**Testing**: xUnit against real PostgreSQL (Order 5434, Catalog 5433); Vitest; Bruno through the storefront's nginx

**Target Platform**: Order and Catalog behind the gateway; the storefront

**Constraints**: no seller id in any request; money never summed across currencies; the period rule shared by every
insight

**Scale/Scope**: grouping in SQL; one round trip per figure

## Design

**Order** (`Application/Insights/InsightsFeatures.cs`, `Infrastructure/.../OrderInsights.cs`)
- `GetSellerRevenueQuery` and `GetSellerTopProductsQuery` return the admin's response shapes
  (`RevenueResponse`, `TopProduct`), so the storefront draws both with one chart.
- The grouping in the handler is shared with the admin's queries: rows in, totals and days out.
- **The rows**, `IOrderInsights.SellerRevenueByDayAsync` and `SellerProductSalesAsync`, are built in SQL:
  - they read `order_items` where `SellerId` is the caller, on an order in `Sold` inside the period;
  - they skip a line whose part (`order_shipments` of `(OrderId, SellerId)`) has a return in `Received`;
  - revenue is `Quantity × UnitPrice`;
  - orders are counted **distinct per day and currency**, because one order can hold several of a seller's
    lines.
- A new `SalesInsightsController`, at `api/orders/sales/insights` with `[Authorize(Roles = "Seller")]`.

**Catalog** (`Application/Products/Views/ProductViewFeatures.cs`)
- `GetMyProductInsightsQuery(From, To, Limit)` returns `SellerProductInsights(Views, RatingAverage,
  RatingCount, Products[ProductId, Name, Views, RatingAverage, RatingCount])`, with the most viewed products
  first.
- The rating is read from `products.RatingAverage` and `RatingCount`. Those columns are already recomputed
  from the visible reviews on every write (specs/046), so the overall figure is
  `Σ(avg × count) / Σ count`. It is null when there are no reviews.
- `IProductViewRepository.SellerAsync(sellerId, from, to, limit)` does the grouping in SQL.
- The route is `GET api/products/insights/mine` with `[Authorize(Roles = "Seller")]`.

**Storefront**
- `Insights.sellerRevenue`, `sellerTopProducts` and `mine`, with the hook `useSellerInsights`.
- The chart moves from `pages/admin-overview/daily-chart.tsx` to `components/insights/daily-revenue-chart`,
  and its words move to `common` (`chart.*`), because it now serves two consoles.
- The new page `pages/shop-insights` is routed at `/shop/insights` and added to the menu.

> **Correction (backfill)**: the code at the merge puts those words under `common` → **`insights.*`**
> (`insights.daily`, `insights.peak`, `insights.orders_one` …), not `chart.*`; the PR says the same. And it was not the
> chart alone that moved: the revenue panel, ranked list, card (`insight-panel`) and period picker moved to
> `client/src/components/insights` with it, and a `periodRange` helper was added in `client/src/utils/insights`.

## Research

- **D1 - a received return is not revenue here.** The admin Overview still counts it: that is a known limit of
  specs/066, and fixing it belongs in its own change. For the seller, the number sits beside earnings that
  already exclude it, and two answers on one console would be read as a bug.
- **D2 - the same response shapes as the admin's.** One chart and one set of types, rather than a parallel
  "seller" family that drifts.
- **D3 - the rating comes from the product's stored average, not from counting reviews again.** It is kept
  right in the transaction of every review write, hide and restore (specs/046). Counting again would be a
  second definition.
- **D4 - the Catalog endpoint takes no seller id.** The caller's id is the owner (Constitution IV), the same
  as `/api/orders/sales`.

With their alternatives in the standard shape: [research.md](research.md).

## Constitution check

- **I (service autonomy):** each service answers from its own data, and the page composes the two answers.
- **IV (identity from the token):** there is no seller id in any request.
- **V (evidence):** tests against the real PostgreSQL, mutation checks, and Bruno checks for 200 as a seller
  and 403 as a customer.

Against all five principles of [constitution.md](../../.specify/memory/constitution.md):

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order answers money from its own frozen lines; Catalog answers views and ratings from its own tables; neither calls the other. The page composes two answers, as the admin Overview does |
| **II. Clean Architecture Layering** | **Pass.** Queries and validators in Application, the SQL in Infrastructure repositories behind `IOrderInsights` / `IProductViewRepository`, controllers that only send through MediatR |
| **III. Atomic Writes and Idempotent Messaging** | **Pass (not applicable).** Read-only: no write and no message |
| **IV. Identity Comes From the Token** | **Pass.** `SellerId()` reads `ICurrentUser.Id` and refuses a token without one rather than reading it as "the shop's own"; no request carries a seller id (tested: `A_caller_without_an_id_is_refused`, and `services/insights` asserts the URLs carry none) |
| **V. Evidence Over Assumption** | **Pass, with one gap stated.** 11 + 6 server tests against real PostgreSQL, 9 of 9 mutations caught, Bruno 219/219 through the storefront. Live revenue was empty because the Bruno seller has no sales - the figures are proven by the tests, not by the live run; clicking through in a browser was not done |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/068-seller-insights/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── contracts/http-api.md
└── checklists/requirements.md
```

### Source Code (touched at the merge)

```text
server/src/Services/Order/
├── Ecommerce.Order.Application/Insights/InsightsFeatures.cs
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs
└── Ecommerce.Order.WebApi/Controllers/SalesInsightsController.cs          # new
server/src/Services/Catalog/
├── Ecommerce.Catalog.Application/Common/Interfaces/IProductViewRepository.cs
├── Ecommerce.Catalog.Application/Products/Views/ProductViewFeatures.cs
├── Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductViewRepository.cs
└── Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs
server/tests/Ecommerce.Order.Tests/SellerInsightsTests.cs                    # 11
server/tests/Ecommerce.Catalog.Tests/SellerProductInsightsTests.cs           # 6
client/src/components/insights/{daily-revenue-chart, insight-panel, period-picker, ranked-list, revenue-panel}/
client/src/{services,hooks,utils}/insights/, pages/shop-insights/, pages/admin-overview/index.tsx
client/src/{routes/index.tsx, layouts/seller-layout/index.tsx, locales/{en,vi}/{common,seller,admin}.json}
bruno/seller/   # 4 requests: 200 for the seller on both services, 403 for a customer on both
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- The admin Overview still counts a returned sale (research D1) - addressed later (specs/084, per
  `docs/features/returns.md`).
- No comparison with a previous period, no conversion rate, no export.
