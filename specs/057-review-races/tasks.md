---
description: "Task list for Review races and own-product reviews"
---

# Tasks: Review races and own-product reviews

> Completed on 2026-09-27, after the feature merged (#140), from the code at that merge, the pull request and
> docs/features/ratings-and-reviews.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - all three failed before the fix, each for the issue's reason.

## Format: `[ID] [P?] [Story] Description`

- [X] T001 [US1] [US2] [US3] Tests first in `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs`: concurrent first reviews; concurrent hides; concurrent restores; a seller reviewing their own product
- [X] T002 [US2] `TryHideAsync` / `TryRestoreAsync` / `DiscardPendingChanges` in `IReviewRepository` + `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ReviewRepository.cs`
- [X] T003 [US1] [US2] [US3] Handlers in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs`
- [X] T004 Mutation checks; docs `docs/features/ratings-and-reviews.md`, `docs/project/*`
- [X] T005 [US1] Replace the catch-and-retry first write with `TryAddFirstAsync` (`INSERT ... ON CONFLICT DO NOTHING` plus `stage`) in `IReviewRepository` and `ReviewRepository.cs`, sharing one `GuardedAsync` with hide and restore; `DiscardPendingChanges` is not in the merged interface (see the plan's correction)
- [X] T006 [P] `CLAUDE.md`, `docs/overview/project-overview.md`, `docs/testing/testing-strategy.md`
- [X] T007 Run the concurrency tests five times: 5/5 green
- [X] T008 Merged as #140 (2026-09-24), closing #127

## Verification recorded in #140

- Failed before the fix: `23505 duplicate key ... IX_product_reviews_ProductId_CustomerId`; several of five
  concurrent hides succeeded; no refusal for the seller.
- `Ecommerce.Catalog.Tests` 160/160 against real PostgreSQL; the concurrency tests 5/5 over five runs.
- Mutations, each restored: insert without `ON CONFLICT` - 2 red; hide without its guard - 2 red; no own-product
  rule - 1 red.

## Notes

T005-T008 were added on 2026-09-27 from the pull request. T002 is kept as written; its `DiscardPendingChanges`
describes the first version, which was replaced during implementation.
