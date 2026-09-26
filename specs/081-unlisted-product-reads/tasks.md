---
description: "Task list for What hangs on a product off the shelf"
---

# Tasks: What hangs on a product off the shelf

> Completed on 2026-09-27, after the feature merged (#169), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Input**: Design documents from `/specs/081-unlisted-product-reads/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. Who may read what is an authorization boundary, which the constitution puts among the things
that need an automated check.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 the public refused, US2 seller and staff still served, US3 a replaced image retires its key

The original list had three tasks, kept below in full: T001 is now T004-T005, T002 is T001-T003 and T006-T011, and
T003 is T012-T015.

---

## Phase 1: Foundational

- [X] T001 Add `ImageAccessKey` (`Guid?`) to `server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/Product.cs` and `ProductVariant.cs`
- [X] T002 Create migration `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260926150608_AddImageAccessKeys.cs`: two nullable `uuid` columns plus `gen_random_uuid()` for every image already stored ("the migration with its backfill")
- [X] T003 Give `TrySetImageAsync` / `TrySetVariantImageAsync` an `accessKey` in `.../Application/Common/Interfaces/IProductRepository.cs` and set it inside the guarded `ExecuteUpdateAsync` in `.../Infrastructure/Persistence/Repositories/ProductRepository.cs`

## Phase 2: Tests first

- [X] T004 [US1] [US2] [US3] Original T001 - tests in `server/tests/Ecommerce.Catalog.Tests/UnlistedProductReadsTests.cs`:
  - an image on sale is served with or without the key;
  - off the shelf it needs its own key;
  - a waiting product's photograph is shown to its seller and staff only;
  - a replacement retires the old key;
  - a variant's own photograph follows the same rule;
  - reviews and questions are a 404 but to the seller and staff, and everybody's while on sale.
- [X] T005 [P] Update `server/tests/Ecommerce.Catalog.Tests/ProductImageTests.cs` for the address ending in `&k=`

## Phase 3: User Story 1 - the public is refused (P1)

- [X] T006 [US1] `GetProductReviewsQuery` asks `ProductReview.MaySee` and throws `Product not found.` in `.../Application/Reviews/ReviewFeatures.cs`
- [X] T007 [P] [US1] `GetProductQuestionsQuery` does the same in `.../Application/Questions/QuestionFeatures.cs`
- [X] T008 [US1] `ProductImageKey.MayServe` and `&k=` in `UrlFor` / `UrlForVariant` in `.../Application/Products/Images/ProductImageKey.cs`; `GetProductImageQuery` takes `Key` and returns `ProductImage.Public` in `.../Products/Images/GetProductImage/GetProductImageQuery.cs`; `GetVariantImageQuery` the same in `.../Products/Images/GetVariantImageQuery.cs`

## Phase 4: User Story 2 - seller and staff still served (P2)

- [X] T009 [US2] Read `k` and write `CacheFor` (`private, no-cache` off the shelf) in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs`

## Phase 5: User Story 3 - a replaced image retires its key (P3)

- [X] T010 [US3] A new `Guid.NewGuid()` per upload in `.../Products/Images/UploadProductImage/UploadProductImageCommand.cs` and in `UploadVariantImageCommandHandler` (`.../Products/Images/VariantImageCommands.cs`)
- [X] T011 [US3] Removal passes a null key in `.../Products/Images/RemoveProductImage/RemoveProductImageCommand.cs` and `RemoveVariantImageCommandHandler`

## Phase 6: Polish and verification

- [X] T012 [P] Original T003, Bruno: `bruno/product/upload image.yml` expects `&k=`; new `bruno/seller/a moderator takes the product down.yml` (seq 78), `its reviews are a 404 to a stranger.yml` (79), `its questions are a 404 to a stranger.yml` (80), `its seller still reads its questions.yml` (81)
- [X] T013 Original T003: the live probe from the issue against the rebuilt stack (four requests, three of them 200 before and 404 after), and five mutations each turning `UnlistedProductReadsTests` red
- [X] T014 [P] Original T003, the docs: `CLAUDE.md`, `docs/features/catalog.md`, `docs/features/ratings-and-reviews.md`, `docs/reference/data-model.md`, `docs/testing/testing-strategy.md`, `docs/overview/project-overview.md`, `docs/project/timeline.md`, `docs/project/backlog.md`
- [X] T015 Merged as #169 on 2026-09-26 (closes #166), after `Ecommerce.Catalog.Tests` 205/205, Bruno 267/267 requests and 433/433 tests, and the four browser flows in Edge

---

## Dependencies & Execution Order

- Phase 1 blocks everything: the column and the repository signature are what every later task compiles against.
- T004 was written before T006-T011 and failed first.
- US1 (T006-T008) before US2 (T009): the cache header reads `ProductImage.Public`, which T008 adds.
- US3 (T010-T011) depends only on T003.
- T006 and T007 are different files and could run in parallel.

## Notes

- 15 tasks: 3 foundational, 2 test, 3 for US1, 1 for US2, 2 for US3, 4 polish.
- The storefront needed no task: it already renders the `imageUrl` the server returns.
