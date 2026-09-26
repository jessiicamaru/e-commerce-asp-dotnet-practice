# Implementation Plan: Admin insights

> Completed on 2026-09-27, after the feature merged (#99, #101), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Branch**: `047-admin-insights` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | [research](research.md)

## Summary

An Overview page for administrators, **composed by the storefront from three services**, each answering from
its own data. Order reports revenue per currency per day, the top products and the top buyers, grouping in
PostgreSQL over one definition of a sale (`OrderInsights.Sold`: Paid, Completed, Preparing, Shipped) and never
adding money across currencies. Catalog starts counting product page views, one counter per product per day,
incremented by a single upsert, and reports the most viewed. Identity turns buyer ids into emails and counts
people by role. No service calls another for any of it, nothing is published or consumed, and the only write in
the whole feature is the view counter. A follow-up (#101) made the daily chart draw every day of the period
rather than only the days that sold.

## Technical Context

**Language/Version**: C# / .NET 10.0; the storefront in React 19 + TypeScript

**Primary Dependencies**: MediatR, FluentValidation, Npgsql EF Core (LINQ grouping translated to SQL; one
interpolated raw statement for the upsert); TanStack Query, react-i18next and shadcn/ui in the client

**Storage**: PostgreSQL 16. One new table, `product_views`, in `ecommerce_catalog_db` (5433). Order
(`ecommerce_order_db`, 5434) and Identity (`ecommerce_identity_db`, 5435) are read only - no migration

**Testing**: xUnit against a real PostgreSQL in Order, Catalog and Identity (the concurrency under test is the
database's upsert); Vitest in the client; Bruno through the gateway; `verify-saga.sh`

**Target Platform**: Linux container / Windows dev host, through the gateway on 5000

**Project Type**: additions to three existing backend services and one client page

**Performance Goals**: not set. The Overview makes six reads and a seventh for emails; no latency was measured
(not recorded)

**Constraints**: one definition of a sale; money never summed across currencies; a view counted once per
request under any concurrency; insights readable by an administrator only

**Scale/Scope**: a top list is at most 50 rows (`limit` 1-50, default 10; the page asks for 5); a revenue period
at most 366 days; a lookup at most 100 ids

The first draft's notes, kept as written apart from one correction:

- **Order**
  - `IOrderInsights` is implemented by `OrderInsights`, which groups in SQL over the orders that count as
    sold.
  - `InsightsHandlers` shapes the results per currency. An order from before specs/022 has no currency
    and counts as the default one.
  - Endpoints, all Admin only:
    - `GET /api/orders/insights/revenue`: totals and a daily series.
    - `GET /api/orders/insights/top-products`: `by=units` or `by=revenue`, with a `currency`.
    - `GET /api/orders/insights/top-buyers`: ids, with spend per currency.
  - A period runs from `from` (inclusive) to `to` (exclusive). The default is the last 30 days; the
    longest allowed is 366 days - *for revenue only*. **Correction (2026-09-27)**: the first draft stated the
    limit generally; at the merge only `GetRevenueQueryValidator` checks `InsightsPeriod.MaxDays`.
    `top-products` and `top-buyers` check only that the period starts before it ends, and Catalog's
    `top-viewed` works in whole days with both ends included. specs/055 (#125) made the four agree.
- **Catalog**
  - `product_views` has one row per product per day, keyed on (ProductId, Day).
  - `POST /api/products/{id}/view` is anonymous and always returns 204. It counts only a listed product
    viewed by a shopper.
  - `GET /api/products/insights/top-viewed` is Admin only.
- **Identity**
  - `GET /api/users/lookup?ids=` returns the email and name for each id.
  - `GET /api/users/stats` returns a count per role, plus how many accounts are locked or banned.
  - Both are Admin only.
- **Client**
  - `services/insights` and `hooks/insights`. Queries are keyed by the start of the period.
  - The product page reports a view once per product opened, using a ref guard.
  - `/admin/overview` is the first item in an administrator's sidebar. It shows headline cards, revenue
    cards per currency with a daily chart for the chosen currency, and three top-5 lists.

## Research

The first draft's three decisions, kept; the full set with rationale and alternatives is in
[research.md](research.md) (D1-D14).

- **D1 - The client composes the page.** The Overview calls Order, Catalog and Identity separately. The
  alternative was a new service-to-service edge only for a report.
- **D2 - A view is its own request, not a side effect of `GET /products/{id}`.** The seller's page reads a
  product once per currency, and the storefront refetches when the tab regains focus. Counting reads
  would count both.
- **D3 - One increment per view, done by the database** (`ON CONFLICT DO UPDATE ... + 1`). Reading the
  count and writing it back would lose views that arrive at the same moment.

## Constitution Check

*GATE: evaluated before research and re-checked after design.* Against
[constitution.md](../../.specify/memory/constitution.md).

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Each service answers from its own data; nothing new is shared between them. Order ranks buyers by id and never learns an email; Identity is asked for emails by the storefront, not by Order. Top-selling names are the ones frozen on Order's own lines; most-viewed names are Catalog's. No new synchronous edge, no new contract, no read of another service's database - the rejected alternative in D1. |
| II - Clean Architecture layering | **Pass, with one deviation recorded in Complexity Tracking.** Dependencies point inward: the Application layers declare `IOrderInsights`, `IProductViewRepository` and the two new `IUserRepository` methods, and the SQL lives in Infrastructure (`OrderInsights`, `ProductViewRepository`, `UserRepository`). Controllers only send through MediatR. The deviation is folder shape: each service keeps its new queries in one file (`Insights/InsightsFeatures.cs`, `Products/Views/ProductViewFeatures.cs`, `Users/UserAdministration.cs`) rather than a folder per use case, and Order's `IOrderInsights` sits beside its queries rather than under `Common/Interfaces/`. |
| III - Atomic writes, idempotent messaging | **Pass.** Reads only, apart from the view counter, which is one idempotent-per-attempt statement: each request is exactly one `INSERT ... ON CONFLICT DO UPDATE`, so an attempt either adds one view or none, and there is no read-then-write to lose an update. No message is published or consumed, so there is no outbox ordering and no redelivery to survive. A retried HTTP call counts again, by design at the merge (D9). |
| IV - Identity from the token | **Pass.** Every insight is Admin only, and a view counts only a shopper. Every read carries `[Authorize(Roles = "Admin")]` (on `InsightsController`, and on `top-viewed`, `users/lookup`, `users/stats`); the view is declared `[AllowAnonymous]` explicitly. Who is viewing - their roles and id, for the staff and own-seller rule - comes from `ICurrentUser`, never from the request. The `ids` of the lookup name the people being looked up, not the caller. |
| V - Evidence over assumption | **Pass.** Order, Catalog and Identity tests, 2 mutation checks, Bruno, and client tests. The concurrency claim (20 simultaneous views count 20) runs against a real PostgreSQL, because the guarantee is the upsert's; revenue is asserted against the stored `TotalAmount` of real orders. Named as unverified: the 403 for a moderator and a customer is shown by Bruno on one endpoint each, not on every endpoint (see spec SC-002); `by=revenue` ordering and the 366-day refusal have no unit test (not recorded as tested). |

**Post-design re-check**: no principle violated. The folder-shape deviation under II is recorded in Complexity
Tracking rather than waived.

## Project Structure

### Documentation (this feature)

```text
specs/047-admin-insights/
├── spec.md
├── plan.md              # this file
├── research.md          # D1-D14
├── data-model.md        # product_views, the migration, what was read and not changed
├── quickstart.md        # validation scenarios
├── contracts/
│   └── http-api.md      # seven endpoints; no messages, no gRPC
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

```text
server/src/Services/Order/
├── Ecommerce.Order.Application/Insights/InsightsFeatures.cs
│       # records (RevenueTotal, RevenueDay, RevenueResponse, CurrencyAmount, TopProduct, TopBuyer),
│       # queries and validators, IOrderInsights + row records, InsightsPeriod, InsightsHandlers
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs   # Sold, SoldIn, three groupings
├── Ecommerce.Order.Infrastructure/DependencyInjection.cs                       # registers IOrderInsights
└── Ecommerce.Order.WebApi/Controllers/InsightsController.cs                   # api/orders/insights, Admin

server/src/Services/Catalog/
├── Ecommerce.Catalog.Domain/Entities/ProductView.cs
├── Ecommerce.Catalog.Application/Common/Interfaces/IProductViewRepository.cs  # + ViewedProduct
├── Ecommerce.Catalog.Application/Products/Views/ProductViewFeatures.cs         # RecordProductViewCommand,
│                                                                               # GetTopViewedQuery, handlers
├── Ecommerce.Catalog.Infrastructure/
│   ├── Configurations/ProductViewConfiguration.cs
│   ├── Migrations/20260923214827_AddProductViews.cs (+ Designer, model snapshot)
│   ├── Persistence/CatalogDbContext.cs                                         # DbSet<ProductView>
│   ├── Persistence/Repositories/ProductViewRepository.cs                      # the upsert, TopAsync
│   └── DependencyInjection.cs
└── Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs                 # {id}/view, insights/top-viewed

server/src/Services/Identity/
├── Ecommerce.Identity.Application/Common/Interfaces/IUserRepository.cs        # GetByIdsAsync, CountAsync
├── Ecommerce.Identity.Application/Users/UserAdministration.cs                 # UserBrief, LookupUsersQuery,
│                                                                               # UserStats, GetUserStatsQuery
├── Ecommerce.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs
└── Ecommerce.Identity.WebApi/Controllers/UsersController.cs                   # lookup, stats

server/tests/
├── Ecommerce.Order.Tests/InsightsTests.cs (+ OrderTestFixture registers IOrderInsights)
├── Ecommerce.Catalog.Tests/ProductViewTests.cs (+ CatalogTestFixture registers IProductViewRepository)
└── Ecommerce.Identity.Tests/UserReportTests.cs

client/src/
├── services/insights/{index.ts, types.ts}          # the Insights class; PERIODS = [7, 30, 90]
├── services/product/index.ts                       # Product.recordView
├── hooks/insights/index.ts                         # useInsights, TOP = 5
├── constants/query-keys/index.ts                   # insights(part, from), people(ids)
├── pages/admin-overview/{index.tsx, index.test.tsx}
├── pages/admin-overview/{daily-chart.tsx, daily-chart.test.tsx}     # #101
├── utils/insights/{index.ts, index.test.ts}         # periodDays, #101
├── pages/product/{index.tsx, index.test.tsx}       # one view per product opened
├── layouts/admin-layout/{index.tsx, index.test.tsx}  # the Overview link, first, administrators only
├── routes/index.tsx                                # /admin/overview
├── locales/{en,vi}/admin.json                      # menu.overview, overview.*
└── test/setup.ts                                   # asyncUtilTimeout 3000 (#101)

bruno/admin-insights/                               # folder seq 15, nine requests
bruno/security-checks/insights without a token is 401.yml
```

Touched outside the services: `CLAUDE.md` (the insights paragraph and the three suites' test counts, in #99).
The gateway needed no change: `/api/orders/{**catch-all}`, `/api/products/{**catch-all}` and
`/api/users/{**catch-all}` already routed every new path.

**Structure Decision**: no new project, service or route. Each part lives in the service that owns its data, in
the layer the constitution names; the page is the only place the three meet.

## Complexity Tracking

| Addition or deviation | Why | Simpler alternative rejected |
| :-- | :-- | :-- |
| Queries grouped in one file per service (`InsightsFeatures.cs`, `ProductViewFeatures.cs`, additions to `UserAdministration.cs`), not a folder per use case; `IOrderInsights` beside them rather than in `Common/Interfaces/` | The record gives no reason (not recorded). What the code shows: Order's three queries share `InsightsPeriod`, the row records and one handler class, and Identity's two sit with the user administration added by specs/043 | A `Queries/<UseCase>/` folder per query, the convention in CLAUDE.md - not recorded why it was not followed |
| A page of six reads plus one for emails | The client composes the page (D1) | One endpoint assembling everything - needs a new service-to-service edge for a report |

## What this feature does not finish

- **Revenue is dated by the day the order was placed, in UTC.** An order stored no payment time; specs/072
  (#116) added `orders.PaidAt`, and specs/082 made a day the shop's own.
- **The four insights disagree about a period at the merge**: Order cuts at instants (`to` exclusive), Catalog
  at whole days (both included), and only revenue has the 366-day limit. The storefront's chart, after #101,
  draws the N days ending today while the request's `from` is N x 24 hours earlier, so the totals can include
  part of a day the chart does not draw. specs/055 (#125, PR #138) made one period rule for all four.
- **A view counts every call.** Nothing identifies a viewer, so a reload or a script inflates "most viewed";
  specs/086 (#173, #178) counts once a day per viewer and limits the endpoint per client.
- **Top viewed does not re-check that a product is still on sale** when it reads the counts.
- **Revenue does not subtract a returned parcel** - returns did not exist yet (specs/066; the Overview follows
  in specs/084).
- **No export, no custom date range, no chart beyond a daily bar per currency** (out of scope in the spec).
- **A seller has no equivalent page** until specs/068, which later moved the chart and panels into
  `client/src/components/insights` to share them.
