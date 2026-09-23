# Feature Specification: A deleted product takes its picture with it

**Feature branch**: `029-delete-product-image`
**Created**: 2026-09-23
**Status**: Draft
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

**Acceptance**
1. The product is still deleted, the event is still published, and the caller still gets 204.
2. The failure is logged with the key, at a level somebody would find.
3. No exception reaches the caller.

### US3 - Replacing an image still leaves exactly one file (P1, regression)

**Acceptance**
1. Three uploads to one product leave one file. This passes today and must keep passing.

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

- **SC-001** Uploading an image, deleting the product, and listing the store finds nothing.
- **SC-002** With the store failing every delete, the product is still deleted.
- **SC-003** The two orphans named in #66 are gone from the running volume, removed by hand.
- **SC-004** All Catalog tests still pass, the replace-path test among them.

## Assumptions

- The product row is the fact and the file is a consequence, so the file may be lost without the
  system becoming wrong. The reverse is not true.
- One Catalog instance, as specs/019 already assumes. Two would each see only their own files, and
  that is a property of the directory store rather than of this change.
