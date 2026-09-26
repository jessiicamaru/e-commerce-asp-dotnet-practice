---
description: "Task list for Honest view counts"
---

# Tasks: Honest view counts

> Completed on 2026-09-27, after the feature merged (#178), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Input**: Design documents from `/specs/086-product-view-limits/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. Concurrency (twenty calls at once) and an authorization-shaped rule (the token beats the body)
are both in the constitution's "needs an automated check" list.

## Format: `[ID] [P?] [Story] Description`

- **[Story]**: US1 a reload is not a second view, US2 a loop is limited, US3 nothing names a viewer

The original five tasks are kept in full: original T001 is T001-T002, T002 is T003-T006, T003 is T007, T004 is
T008-T009, T005 is T010-T012.

---

## Phase 1: User Story 2 - the gateway (P2, independent of the rest)

- [X] T001 [US2] Gateway: the `views` policy (30 a minute) in `server/src/ApiGateway/Ecommerce.ApiGateway/AuthRateLimits.cs`, and a `catalog-product-view-route` for `POST /api/products/{id}/view` in `.../Ecommerce.ApiGateway/appsettings.json`
- [X] T002 [US2] A test in `server/tests/Ecommerce.ApiGateway.Tests/AuthRateLimitTests.cs` that the third call over a limit of two is 429, another client still counts, and `GET` and `/saved` are never limited

## Phase 2: User Stories 1 and 3 - Catalog

- [X] T003 [US3] `ProductViewer` in `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/ProductViewer.cs` and `product_viewers` (`.../Infrastructure/Configurations/ProductViewerConfiguration.cs`, `CatalogDbContext.ProductViewers`, migration `.../Migrations/20260926174014_AddProductViewers.cs`, which only adds)
- [X] T004 [US1] [US3] `RecordAsync(productId, day, viewer)` as one statement, with the claim, the count and the pruning of earlier days together, in `.../Application/Common/Interfaces/IProductViewRepository.cs` and `.../Infrastructure/Persistence/Repositories/ProductViewRepository.cs`
- [X] T005 [US1] The viewer hash taken from the token, or else from the body (`ViewerOf`, `RecordProductViewCommand.Viewer`, `ProductViewRequest`) in `.../Application/Products/Views/ProductViewFeatures.cs`
- [X] T006 [US1] An optional body on the controller (`EmptyBodyBehavior.Allow`) in `.../WebApi/Controllers/ProductsController.cs`
- [X] T007 [US1] [US3] Tests in `server/tests/Ecommerce.Catalog.Tests/ProductViewTests.cs`:
  - twenty visitors at once count twenty;
  - one visitor twenty times at once counts once;
  - a signed-in person counts once whatever visitor id they send;
  - naming nobody counts every time;
  - earlier days' viewers are dropped (and the stored viewer is a 64-hex hash).

  The existing view tests now use distinct visitors.

## Phase 3: User Story 1 - the storefront

- [X] T008 [US1] Storefront: `visitorId()` in `client/src/utils/shared/visitor.ts` (exported from `utils/shared`), sent by `Product.recordView` in `client/src/services/product/index.ts`
- [X] T009 [US1] Tests in `client/src/services/product/index.test.ts` cover:
  - the same id every time;
  - a stored id reused, and one that is not an id replaced;
  - none sent when storage is blocked.

## Phase 4: Verification and docs

- [X] T010 Five mutations: no viewer, the body winning over the token, no pruning, no gateway policy, no reuse of the stored id - each turned its tests red
- [X] T011 [P] Bruno: the same visitor opens the product twice, and "most viewed" sees one view (`bruno/admin-insights/a shopper opens the product page.yml`, `the most viewed products.yml`); docs: admin insights, security, the architecture's rate limits, CLAUDE.md, decisions, the reference, counts, timeline and backlog
- [X] T012 Merged as #178 on 2026-09-26 UTC (closes #173), after Catalog 210/210, ApiGateway 13/13, client 460/460 with lint and type-check, and Bruno 268/268 requests and 439/439 tests

---

## Dependencies & Execution Order

- Phase 1 (gateway) shares no file with the rest and could run in parallel with Phase 2.
- T003 before T004; T004 before T005 (the handler passes the viewer to the new signature); T006 after T005.
- T008 depends only on the endpoint accepting a body (T006); an older Catalog ignores it.
- Phase 4 last.
