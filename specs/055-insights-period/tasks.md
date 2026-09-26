---
description: "Task list for One insights period"
---

# Tasks: One insights period

> Completed on 2026-09-27, after the feature merged (#138), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - each failed before the fix.

## Format: `[ID] [P?] [Story] Description`

- [X] T001 [US2] `server/src/BuildingBlocks/Ecommerce.Shared/Insights/InsightsPeriod.cs`
- [X] T002 [US1] [US2] Tests first: `server/tests/Ecommerce.Order.Tests/InsightsTests.cs` (limit on all three, whole-day ends), `server/tests/Ecommerce.Catalog.Tests/ProductViewTests.cs` (limit, whole days), `client/src/pages/admin-overview/index.test.tsx` (asks for exactly the drawn days)
- [X] T003 Order: `Insights/InsightsFeatures.cs` uses the shared rule; Catalog: `Products/Views/ProductViewFeatures.cs`; client `pages/admin-overview/index.tsx`
- [X] T004 Mutation checks; docs `docs/features/admin-insights.md`, `docs/project/*`
- [X] T005 [P] [US2] Bruno `bruno/admin-insights/a period longer than 366 days is refused.yml` (top products, 400)
- [X] T006 [P] `CLAUDE.md`, `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`
- [X] T007 Merged as #138 (2026-09-24), closing #125

## Verification recorded in #138

- Failed before the fix: Order - whole-day ends, a single day, the limit and the message on all three queries;
  Catalog - top viewed snaps a time to its day and refuses more than 366 days; client - `expected 8 to be 7`.
- After: Order 183/183 and Catalog 155/155 against real PostgreSQL; client 282/282; lint and `tsc` clean.
- Mutations, each restored: `End` is the last day - 2 red; top products without the rule - 1 red; no day limit -
  1 red.

## Notes

T005-T007 were added on 2026-09-27 from the pull request; the work was part of #138 but had no task line.
