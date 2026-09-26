# Implementation Plan: Insights in the shop's days

> Written on 2026-09-27, after the feature merged (#170), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Branch**: `082-insights-local-days` | **Date**: 2026-09-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/082-insights-local-days/spec.md`

## Summary

Give the shop one calendar. `InsightsCalendar` in `Ecommerce.Shared/Insights` holds the configured zone
(`Insights:TimeZone`, `Asia/Ho_Chi_Minh` by default), answers which day an instant falls on and when a day begins,
and is registered by `AddInsightsCalendar`, which refuses an unknown zone at startup. `InsightsPeriod.Resolve` and
`ValidPeriod` take it, so every insight's period is whole shop days. Order groups revenue per day with
`TimeZoneInfo.ConvertTimeBySystemTimeZoneId`, which Npgsql writes as `AT TIME ZONE`; Catalog records a view on the
shop's today. The revenue response names `firstDay`/`lastDay` and the storefront's chart draws exactly those. No
migration.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript (React 19) in the storefront

**Primary Dependencies**: FluentValidation 12.1.1, MediatR 12.4.1, EF Core with Npgsql (its `AT TIME ZONE`
translation), `TimeZoneInfo` (IANA ids, Windows-to-IANA conversion)

**Storage**: PostgreSQL 16 - `ecommerce_order_db` (5434) and `ecommerce_catalog_db` (5433); no schema change

**Testing**: xUnit against a real PostgreSQL (`Ecommerce.Order.Tests`, `Ecommerce.Catalog.Tests`); Vitest in
`client/`; Bruno through the gateway

**Target Platform**: Order (5059) and Catalog (5057) services, the storefront

**Project Type**: A shared building block plus changes in two services and the client

**Performance Goals**: None stated

**Constraints**: One period rule for every insight, unchanged in meaning apart from the zone (specs/055); an
older storefront and an older Order must keep working together with newer ones

**Scale/Scope**: Five insight queries in Order, two in Catalog, one command (record a view), two pages

## Constitution Check

*GATE: evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. Re-checked after design.*

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Each service reads its own data. The calendar is cross-cutting infrastructure in `Ecommerce.Shared`, which the principle allows, and each service configures and resolves it itself |
| **II. Clean Architecture Layering** | **Pass.** Validators and handlers (Application) take the calendar by injection; the `AT TIME ZONE` grouping stays in Infrastructure's `OrderInsights`, which receives the zone id as a string rather than the calendar |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** No message changes. The one write, recording a view, is still one upsert statement; only the day it writes changed |
| **IV. Identity Comes From the Token** | **Pass.** Unchanged: a seller's figures still read the seller from `ICurrentUser`; the zone is configuration, never a request value |
| **V. Evidence Over Assumption** | **Pass, with one gap named.** The new Order test runs the real `AT TIME ZONE` grouping against PostgreSQL; Bruno asserts the Hanoi midnight against the rebuilt containers; two mutations were run. The gap, stated in the pull request: Catalog's view day reads `DateTime.UtcNow`, so a mutation recording the UTC day is caught only between 17:00 and 24:00 UTC |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/082-insights-local-days/
├── spec.md
├── plan.md              # This file
├── research.md          # Six decisions
├── data-model.md        # No schema change; what the stored day now means
├── quickstart.md
├── contracts/
│   └── http-api.md      # The period rule and the revenue response's new fields
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/Ecommerce.Shared/Insights/
├── InsightsCalendar.cs          # new: DayOf, StartOf, ZoneId, AddInsightsCalendar
└── InsightsPeriod.cs            # Start/End become fields; Resolve and ValidPeriod take the calendar
server/src/Services/Order/
├── Ecommerce.Order.Application/Insights/InsightsFeatures.cs     # validators, handlers, RevenueResponse
├── Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderInsights.cs   # AT TIME ZONE grouping
└── Ecommerce.Order.WebApi/Program.cs                            # AddInsightsCalendar
server/src/Services/Catalog/
├── Ecommerce.Catalog.Application/Products/Views/ProductViewFeatures.cs
└── Ecommerce.Catalog.WebApi/Program.cs                          # AddInsightsCalendar
server/tests/Ecommerce.Order.Tests/{InsightsTests,SellerInsightsTests,OrderTestFixture}.cs
server/tests/Ecommerce.Catalog.Tests/{ProductViewTests,CatalogTestFixture}.cs
client/src/utils/insights/index.ts (+ index.test.ts)             # periodDays(first, last)
client/src/services/insights/types.ts                            # firstDay?, lastDay?
client/src/pages/admin-overview/index.tsx (+ index.test.tsx), client/src/pages/shop-insights/index.tsx
bruno/admin-insights/revenue per currency.yml, bruno/seller/a seller sees their own revenue.yml
```

Docs in the same change: `CLAUDE.md`, `docs/features/admin-insights.md`, `docs/features/seller-insights.md`,
`docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`, `docs/project/timeline.md`,
`docs/project/backlog.md`.

**Structure Decision**: the calendar joins `InsightsPeriod` in `Ecommerce.Shared/Insights`, because the period rule
is already shared there (specs/055) and both services must agree on what a day is.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- One zone for the whole shop: a reader elsewhere still reads the shop's days.
- Views recorded before this keep their UTC day.
- The view day is not tested at every hour (see Principle V above).
