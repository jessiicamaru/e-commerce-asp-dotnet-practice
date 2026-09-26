# Implementation Plan: One insights period

> Completed on 2026-09-27, after the feature merged (#138), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Branch**: `055-insights-period` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #125

## Summary

Move "what a period is" into `Ecommerce.Shared.Insights.InsightsPeriod` - whole UTC days, both ends included,
default 30, at most 366 - and make revenue, top products, top buyers (Order) and top viewed (Catalog) resolve and
validate through it. The Overview asks for today and the N−1 days before it, so its chart and its totals cover
the same days. Parameters stay `DateTime`.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19

**Primary Dependencies**: FluentValidation 12.1.1 (the shared rule), MediatR, EF Core; client TanStack Query,
Vitest

**Storage**: none new - `orders` (Order, 5434) and `product_views` (Catalog, 5433) are queried as before

**Testing**: xUnit against real PostgreSQL (`Ecommerce.Order.Tests`, `Ecommerce.Catalog.Tests`); Vitest for the
Overview; one Bruno request

**Target Platform**: Order (5059), Catalog (5057), the storefront's `/admin/overview`

**Constraints**: no breaking change to query parameters; each service still answers from its own data

**Scale/Scope**: one shared type, four validators, three handlers, one client line

## Design

`InsightsPeriod(DateOnly FirstDay, DateOnly LastDay)` lives in `Ecommerce.Shared/Insights`.

| Member | Meaning |
| :-- | :-- |
| `Start` | `FirstDay` at 00:00 UTC |
| `End` | **exclusive**: the day after `LastDay`, at 00:00 UTC |
| `Days` | the number of days, both ends included |
| `Resolve(from, to, now)` | the period a request names |
| `ValidPeriod(x => x.From, x => x.To)` | a FluentValidation rule builder, used by all four validators |

- **Order:** the repository queries keep `>= from && < to`, fed `Start` and `End`. The revenue response
  reports `Start` and `End`.
- **Catalog:** top viewed is fed `FirstDay` and `LastDay`, and was already inclusive.
- **Client:** `from` = today minus (N−1) days and `to` = now. `periodDays(to, N)` then draws exactly the
  days asked for.

**Why whole days.** Revenue is grouped by day, views are counted by day, and the chart is drawn by day.
A period cut mid-day always leaves one bar that counts part of a day.

At the merge `InsightsPeriod` is a `readonly record struct` with `MaxDays = 366` and `DefaultDays = 30`;
`ValidPeriod` is an extension method on `AbstractValidator<T>` in `InsightsPeriodRules`, validating the resolved
period, with the display name `Period`. Order's own `InsightsPeriod` class (a `(From, To)` tuple and a `NotAfter` rule)
was deleted.

## Constitution Check

*Evaluated against [constitution.md](../../.specify/memory/constitution.md); re-checked after design.*

- I (autonomy): each service still answers from its own data. Only the rule is shared, the way
  `StaffRoles` is. Pass.
- V (evidence): the limit test fails first on top products, top buyers and top viewed. The snapping test
  fails first at both ends. The client test fails first on the N+1 request. Pass.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Order answers from `orders`, Catalog from `product_views`; the client composes. `InsightsPeriod` is a cross-cutting rule in `Ecommerce.Shared`, the place the principle names for such code, like `StaffRoles` |
| **II. Clean Architecture Layering** | **Pass.** Resolution happens in the Application handlers and validators; the repositories keep taking plain bounds. `Ecommerce.Shared` gains a dependency on FluentValidation it already had for `ValidationBehavior` |
| **III. Atomic Writes and Idempotent Messaging** | **Pass, not engaged.** Read-only queries; no message |
| **IV. Identity Comes From the Token** | **Pass.** All four endpoints stay `Admin` only (`[Authorize(Roles = "Admin")]`); no identity in any parameter |
| **V. Evidence Over Assumption** | **Pass.** Each new test failed before the fix against a real PostgreSQL (whole-day ends, a single day, the limit and message on all three Order queries; snapping and the limit on top viewed) and the client test failed with `expected 8 to be 7`; three mutations were each caught |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/055-insights-period/
├── spec.md
├── plan.md            # This file
├── research.md        # D1-D4
├── data-model.md      # No schema change; the rule as a value
├── quickstart.md
├── contracts/
│   └── http-api.md    # The four insight endpoints' period parameters and refusals
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code

- `server/src/BuildingBlocks/Ecommerce.Shared/Insights/InsightsPeriod.cs` (new)
- `server/src/Services/Order/Ecommerce.Order.Application/Insights/InsightsFeatures.cs`
- `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Views/ProductViewFeatures.cs`
- `client/src/pages/admin-overview/index.tsx`, `index.test.tsx`
- `server/tests/Ecommerce.Order.Tests/InsightsTests.cs`, `server/tests/Ecommerce.Catalog.Tests/ProductViewTests.cs`
- `bruno/admin-insights/a period longer than 366 days is refused.yml` (new)
- `CLAUDE.md`, `docs/features/admin-insights.md`, `docs/overview/project-overview.md`,
  `docs/testing/testing-strategy.md`, `docs/project/backlog.md`, `docs/project/timeline.md`

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

## What this feature does not finish

- Revenue is still dated by when the order was placed (`CreatedAt`), not when it was paid; specs/072 changed that.
