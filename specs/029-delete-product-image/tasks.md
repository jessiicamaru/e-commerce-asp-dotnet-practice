# Tasks: A deleted product takes its picture with it

## Phase 1: The red test

- [ ] T001 `Deleting_a_product_deletes_its_image` in server/tests/Ecommerce.Catalog.Tests/ProductImageTests.cs
- [ ] T002 `A_failing_store_does_not_stop_a_product_being_deleted` in the same file, using `FailDeletes`
- [ ] T003 Run both and record that T001 fails on today's code, naming the assertion

## Phase 2: The fix

- [ ] T004 `DeleteProductCommandHandler` takes `IProductImageStore` in server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Commands/DeleteProduct/DeleteProductCommand.cs
- [ ] T005 Read `ProductImageKey.For(product)` beside the variant ids, before the row is removed
- [ ] T006 After `SaveChangesAsync`, delete the key in a try/catch that logs a warning naming the key
- [ ] T007 Run the Catalog suite: 111 tests, all green

## Phase 3: The evidence

- [ ] T008 Verify on the running stack: upload, delete, list the volume
- [ ] T009 Delete the two orphans from #66 by hand, after confirming each product is 404

## Phase 4: Say so

- [ ] T010 One sentence in the specs/019 paragraph of CLAUDE.md about deletion
- [ ] T011 File the reconciliation sweeper as its own issue, referencing research D4
- [ ] T012 PR closing #66
