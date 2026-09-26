---
description: "Task list for Delete a Product"
---

# Tasks: Delete a Product

> Written on 2026-09-27, after the feature merged (#61), from the code at that merge, the pull request and docs/features/catalog.md.

**Input**: Design documents from `/specs/024-delete-product/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. The claims are about the database (rows cascading, a unique SKU freed, rows
dropped) and about what is published, so both test classes run against a real PostgreSQL.

Reconstructed from the merge diff; every task below is in #61.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task serves (US1-US3)

---

## Phase 1: Foundational

- [X] T001 Add `ProductDeletedEvent(Guid ProductId, IReadOnlyList<Guid> VariantIds, DateTime DeletedAt)` in `server/src/BuildingBlocks/Ecommerce.Contracts/Catalog/ProductDeletedEvent.cs`, documenting that it is not "stop selling" and that orders are deliberately not told

---

## Phase 2: User Story 1 - An administrator removes a product (P1) 🎯 MVP

- [X] T002 [P] [US1] Write `DeleteProductTests` in `server/tests/Ecommerce.Catalog.Tests/DeleteProductTests.cs`: product, variants, options, prices and translations gone; every variant announced; 404 for a missing product; the SKU reusable
- [X] T003 [US1] Add `void Remove(Product product)` to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductRepository.cs`
- [X] T004 [US1] Implement `Remove` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Persistence/Repositories/ProductRepository.cs`: `RemoveRange(product.Variants)` explicitly (the key is `RESTRICT`), then the product
- [X] T005 [US1] Implement `DeleteProductCommand` and its handler in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Commands/DeleteProduct/DeleteProductCommand.cs`: 404 when missing, collect variant ids before the delete, stage, publish `ProductDeletedEvent`, save once, log a warning
- [X] T006 [US1] Add `DELETE {id:guid}` with `[Authorize(Roles = "Admin")]` to `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Controllers/ProductsController.cs`, returning 204
- [X] T007 [P] [US1] Add Bruno `bruno/product/create a product to delete.yml`, `bruno/product/delete a product.yml` (204) and `bruno/product/a deleted product is gone.yml` (404)
- [X] T008 [P] [US1] Add Bruno `bruno/security-checks/customer cannot delete a product (403).yml`, aimed at the collection's real product

**Checkpoint**: a product can be deleted through the gateway and is gone.

---

## Phase 3: User Story 2 - Stock for a deleted product does not stay on the shelf (P2)

- [X] T009 [P] [US2] Write `ForgetProductTests` in `server/tests/Ecommerce.Inventory.Tests/ForgetProductTests.cs`: 404 not zero; every variant forgotten; twice is a no-op; bystanders untouched; an empty list touches nothing
- [X] T010 [US2] Add `Task<int> ForgetAsync(IEnumerable<Guid> productIds, ...)` to `server/src/Services/Inventory/Ecommerce.Inventory.Application/Common/Interfaces/IStockRepository.cs`
- [X] T011 [US2] Implement `ForgetAsync` as one `ExecuteDeleteAsync` in `server/src/Services/Inventory/Ecommerce.Inventory.Infrastructure/Persistence/Repositories/StockRepository.cs`
- [X] T012 [US2] Implement `ForgetProductCommand` and its handler in `server/src/Services/Inventory/Ecommerce.Inventory.Application/Stock/Commands/ForgetProduct/ForgetProductCommand.cs`
- [X] T013 [US2] Implement `ProductDeletedConsumer` in `server/src/Services/Inventory/Ecommerce.Inventory.WebApi/Consumers/ProductDeletedConsumer.cs`, sending `ForgetProductCommand(VariantIds)`
- [X] T014 [US2] Register it with `x.AddConsumer<ProductDeletedConsumer>()` in `server/src/Services/Inventory/Ecommerce.Inventory.WebApi/Program.cs`, with the comment warning that an unregistered consumer never runs and never complains - the step that was first missed ([research.md D5](./research.md))

**Checkpoint**: a deleted product's variants answer 404 on `GET /api/stock/{id}` within seconds.

---

## Phase 4: User Story 3 - Clean a test-soiled catalogue (P3)

- [X] T015 [US3] Write `server/seed/clean-test-debris.py`: keep what `seed/cameras.json` names, list the rest, delete only with `--yes`, through `DELETE /api/products/{id}` as an administrator, counting failures separately
- [X] T016 [US3] Run the cleaner against the development catalogue: 97 deleted, 14 left (recorded in the PR)

---

## Phase 5: Polish

- [X] T017 [P] Document the cleaner and its keep-list reasoning, and update the test counts (Inventory 31, Catalog 82), in `CLAUDE.md`
- [X] T018 Verify against the running stack: a probe product answered 200 on `GET /api/stock/{id}`, was deleted, and answered 404 four seconds later; count stock rows against variants (125 against 23), which found the unregistered consumer (T014)
- [X] T019 Run every test project (251 pass) and Bruno (85/85 requests, 126 tests)
- [X] T020 Write this design record retrospectively under `specs/024-delete-product/` (2026-09-27)
- [X] T021 Merged as [#61](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/61) (`2223f0d`) on 2026-09-22

---

## Dependencies & Execution Order

- T001 blocks T005 and T013 (both name the event).
- US1 (T002-T008) and US2 (T009-T014) touch different services and could run in parallel once T001 exists.
- US3 depends on US1 (it calls the endpoint) and, to leave no orphaned stock, on T014 - which is the
  ordering that was broken at the time.

## Notes

- 21 tasks: 1 foundational, 7 for US1, 6 for US2, 2 for US3, 5 polish.
- 2 test tasks (T002, T009) holding 9 tests.
