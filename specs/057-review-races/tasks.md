# Tasks: Review races and own-product reviews

- [ ] T001 [US1] [US2] [US3] Tests first in `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs`: concurrent first reviews; concurrent hides; concurrent restores; a seller reviewing their own product
- [ ] T002 [US2] `TryHideAsync` / `TryRestoreAsync` / `DiscardPendingChanges` in `IReviewRepository` + `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ReviewRepository.cs`
- [ ] T003 [US1] [US2] [US3] Handlers in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Reviews/ReviewFeatures.cs`
- [ ] T004 Mutation checks; docs `docs/features/ratings-and-reviews.md`, `docs/project/*`
