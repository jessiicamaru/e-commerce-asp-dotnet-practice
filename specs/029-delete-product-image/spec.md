# Feature Specification: A deleted product takes its picture with it

> Completed on 2026-09-27, after the feature merged (#68), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature branch**: `029-delete-product-image`
**Created**: 2026-09-23
**Status**: Merged as [#68](https://github.com/jessiicamaru/e-commerce-asp-dotnet-practice/pull/68) on 2026-09-23 (written as Draft; corrected on 2026-09-27)
**Closes**: #66

## What is wrong

Deleting a product removes its row and leaves its image file on the `catalog_images` volume
**forever**. Nothing ever reclaims it. The shop keeps working perfectly, which is the problem: there
is no symptom until the volume has grown for reasons nobody can account for.

Two orphans were on the running stack when this was found, both from the same day, both belonging
to products that answer 404. They were left there deliberately as evidence.

`DeleteProductCommandHandler` never asks for `IProductImageStore` - its constructor takes
`IProductRepository`, `IPublishEndpoint`, `ICurrentUser` and `ILogger`, and that is all. Nothing
else sweeps the directory.

## What is NOT wrong

Checked on the running stack before writing this, because the fix would otherwise have been aimed
at the wrong place:

- **Replacing an image already deletes the old file.** Three uploads to one product left exactly one
  file on disk. `UploadProductImageCommand` writes the new bytes, switches the row with a guarded
  `UPDATE`, and only then deletes the old key.
- **`DELETE /api/products/{id}/image` already deletes the file** (204, verified), and already logs
  and swallows a store failure, with a comment saying the file is left behind as an orphan.

So three of the four ways an image can become unreachable are correct. **One command is wrong**, and
the two correct ones are the design to copy rather than to reconsider.

## User Scenarios

### US1 - Deleting a product reclaims its bytes (P1)

An administrator or the owning seller deletes a product that has an image. The row goes, the
announcement goes out, and the file is gone from the store.

**Why this priority**: It is the defect in #66 - the one write path of four that leaks.

**Independently testable**: upload an image, delete the product, ask the store whether the key is
still there.

**Acceptance**
1. After the delete, the store holds no file for that product.
2. A product with no image deletes exactly as it does today, with no call to the store.
3. The `ProductDeletedEvent` still carries the product id and every variant id, and Inventory still
   drops the stock rows - nothing about the announcement changes.
4. The order that bought the product is untouched: it froze the name, price, sku and option summary.

### US2 - A failing store does not block a deletion (P1)

The volume is read-only, or the file is already gone, or object storage is having a bad minute.

**Why this priority**: Without it, the fix to US1 would make a leftover PNG able to stop an
administrator removing a product - a worse defect than the leak.

**Independently testable**: set the test store to fail every delete, delete a product with an image,
and observe 204 and the product gone.

**Acceptance**
1. The product is still deleted, the event is still published, and the caller still gets 204.
2. The failure is logged with the key, at a level somebody would find.
3. No exception reaches the caller.

### US3 - Replacing an image still leaves exactly one file (P1, regression)

**Why this priority**: The replace path was already correct; this story exists so the change cannot
quietly break it.

**Independently testable**: upload three images to one product and list the store.

**Acceptance**
1. Three uploads to one product leave one file. This passes today and must keep passing.

## Edge Cases

- **A product with no image.** `ProductImageKey.For` returns null and the store is never called.
- **The store fails** (read-only volume, a transient error). Logged with the key and swallowed; the
  deletion stands (FR-003).
- **The file is already gone.** `DeleteAsync` promises that removing what is not there is not an error
  (research D6).
- **A crash between the save and the file delete.** The product is gone and the file stays - an orphan,
  bounded to that one crash. Accepted; recovery is the sweeper in #67 (research D4).
- **A concurrent replacement of the same product's image.** Named in the checklist as considered, but no
  test covers it and the record does not say how it resolves. The delete removes the key it read before
  removing the row; an upload whose guarded switch finds no row deletes its own new file and answers 409
  (specs/019). What remains in the worst interleaving is at most an orphan, which is the state this
  feature already accepts.
- **A variant's own photograph.** Did not exist at this merge (specs/032 added them, and extended this
  handler to delete them too).
- **An order for the deleted product.** Loses its picture, as it already did on image removal or
  replacement (research D1).

## Key Entities

- **Product image**: bytes in `IProductImageStore` under a key derived from the product row -
  `{productId:N}-{ImageUpdatedAt ticks}.{ext}`. No column names the file; the row's `ImageContentType` and
  `ImageUpdatedAt` are the whole reference (see [data-model.md](./data-model.md)).
- **Orphan**: a file in the store that no row's key names.

## Requirements

- **FR-001** Deleting a product MUST delete its image from `IProductImageStore`.
- **FR-002** The row MUST go first and the bytes second. Losing the bytes after the row is gone is
  harmless; losing them first would leave a live row naming a missing file for as long as the
  transaction lasts, which is the failure the upload ordering exists to prevent.
- **FR-003** A store failure during a delete MUST be logged and swallowed. A product that cannot be
  deleted because of a leftover PNG is a worse defect than the leak this fixes.
- **FR-004** The image key MUST be read from the product before the row is removed, the way the
  variant ids already are.
- **FR-005** Nothing here may assume a POSIX filesystem. `IProductImageStore` is the seam object
  storage plugs into, and `DeleteAsync` already promises that removing what is not there is not an
  error.
- **FR-006** A test MUST fail on today's code and pass after the change.

## Out of scope

- **A reconciliation sweeper** for images already orphaned - by this bug, by a crash between the two
  steps, or by a store failure swallowed under FR-003. It is the honest completion of this work and
  it is also the thing that deletes a live product's image if its query is wrong. Separate issue.
- **Putting a copy of the image on the order.** An order page for a deleted product loses its
  picture; see research D1 for why that is accepted rather than overlooked.
- **The front end.** Nothing in `client/` is involved.

## Success Criteria

Measured at the merge (from the PR): SC-001 by `Deleting_a_product_deletes_its_image` and on the
running stack (3 files → 2 after the delete); SC-002 by `A_failing_store_does_not_stop_a_product_being_deleted`
with a negative control; SC-003 by hand (volume left empty); SC-004 with Catalog at 111 tests.


- **SC-001** Uploading an image, deleting the product, and listing the store finds nothing.
- **SC-002** With the store failing every delete, the product is still deleted.
- **SC-003** The two orphans named in #66 are gone from the running volume, removed by hand.
- **SC-004** All Catalog tests still pass, the replace-path test among them.

## Assumptions

- The product row is the fact and the file is a consequence, so the file may be lost without the
  system becoming wrong. The reverse is not true.
- One Catalog instance, as specs/019 already assumes. Two would each see only their own files, and
  that is a property of the directory store rather than of this change.
