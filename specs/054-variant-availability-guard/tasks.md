---
description: "Task list for Variant availability guard"
---

# Tasks: Variant availability guard

> Completed on 2026-09-27, after the feature merged (#137), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - the ordering test failed before the fix.

## Format: `[ID] [P?] [Story] Description`

- [X] T001 [US1] Test first in `server/tests/Ecommerce.Catalog.Tests/AvailabilityTests.cs`: t1 in stock, t3 in stock, t2 out of stock last - stays in stock at t3
- [X] T002 [US1] Remove the value clause in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs`
- [X] T003 Mutation check; docs `docs/features/catalog.md`, `docs/project/*`
- [X] T004 [P] `CLAUDE.md`, `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md` (touched by the pull request)
- [X] T005 Merged as #137 (2026-09-24), closing #124

## Verification recorded in #137

- The new test failed before the fix; `Ecommerce.Catalog.Tests` 153/153 against real PostgreSQL after it,
  including the existing duplicate, overtaken and newer-wins tests.
- The mutation check is the original code with the clause re-added - the red run above.

## Notes

T004 and T005 were added on 2026-09-27 from the pull request. The same pull request's timeline change also
recorded #136 (the OpenAPI advisory), which is not part of this feature.
