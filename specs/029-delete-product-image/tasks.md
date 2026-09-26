---
description: "Task list for A deleted product takes its picture with it"
---

# Tasks: A deleted product takes its picture with it

> Completed on 2026-09-27, after the feature merged (#68), from the code at that merge, the pull request and docs/features/catalog.md.

**Input**: Design documents from `/specs/029-delete-product-image/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included, and required to be seen red first (spec FR-006).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US3). The original tasks carried no story label; the
  mapping is: T001, T004-T006, T008 serve US1; T002, T013 serve US2; T007 covers US3's regression test.

## Phase 1: The red test

- [X] T001 `Deleting_a_product_deletes_its_image` in server/tests/Ecommerce.Catalog.Tests/ProductImageTests.cs
- [X] T002 `A_failing_store_does_not_stop_a_product_being_deleted` in the same file, using `FailDeletes`
- [X] T003 Run both and record that T001 fails on today's code, naming the assertion

## Phase 2: The fix

- [X] T004 `DeleteProductCommandHandler` takes `IProductImageStore` in server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Commands/DeleteProduct/DeleteProductCommand.cs
- [X] T005 Read `ProductImageKey.For(product)` beside the variant ids, before the row is removed
- [X] T006 After `SaveChangesAsync`, delete the key in a try/catch that logs a warning naming the key
- [X] T007 Run the Catalog suite: 111 tests, all green

## Phase 3: The evidence

- [X] T008 Verify on the running stack: upload, delete, list the volume
- [X] T009 Delete the two orphans from #66 by hand, after confirming each product is 404

## Phase 4: Say so

- [X] T010 One sentence in the specs/019 paragraph of CLAUDE.md about deletion
- [X] T011 File the reconciliation sweeper as its own issue, referencing research D4 -> #67
- [X] T012 PR closing #66

## Added while building

- [X] T013 Negative control on the swallow: removing the try/catch makes
      `A_failing_store_does_not_stop_a_product_being_deleted` fail with `IOException: Simulated
      storage failure`, so the test guards the behaviour rather than passing because nothing calls
      the store. It DID pass before the fix, for exactly that wrong reason.

## Completed on 2026-09-27

- [X] T014 [P] Record what the image data is and the order of a deletion in `specs/029-delete-product-image/data-model.md`, and complete the plan, research and spec to the specs/001 standard
- [X] T015 Merged as [#68](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/68) (`70f7f67`) on 2026-09-23, closing #66
