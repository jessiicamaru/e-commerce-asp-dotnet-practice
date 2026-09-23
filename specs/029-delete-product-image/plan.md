# Implementation Plan: A deleted product takes its picture with it

**Branch**: `029-delete-product-image` | **Spec**: [spec.md](spec.md) | **Closes**: #66

## Technical Context

One handler, one dependency, one try/catch, two tests. `Ecommerce.Catalog.Tests` already has a
controllable store (`TestImageStore`) with `FailDeletes` and a `Files()` listing, so both scenarios
are expressible without new test infrastructure.

**No data model change. No contract change. No migration. No client change.**

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Untouched. Catalog deletes its own bytes; no new cross-service call. |
| II - Clean Architecture | The handler talks to `IProductImageStore`, an Application-layer interface. It must not touch a directory - FR-005. |
| III - Atomic writes, idempotent messaging | **The delicate one.** The file delete sits *outside* the transaction, deliberately: it is not part of the atomic write and must not be able to fail it (D2, D3). The row and the `ProductDeletedEvent` stay in one transaction exactly as today, and nothing about the outbox changes. A redelivery is unaffected - no consumer consults the store. |
| IV - Identity from the token | Unchanged. `SellerOwnership.RequireCanWrite` still runs before anything is removed. |
| V - Evidence over assumption | The test goes **red first** and is shown doing so. The claim that the replace path is already correct was measured on the running stack - three uploads, one file - not inferred from the code. |

No Complexity Tracking entries.

## The trap this change must not fall into

**Making the deletion depend on the store.** The instinct is to treat "the file is gone" as part of
the operation and let a failure abort it. That turns a leaked PNG into a product that cannot be
removed from the catalogue - and `DELETE /api/products/{id}` exists precisely for rows that should
never have existed (specs/024). The correct shape is the one `RemoveProductImageCommandHandler`
already has, and the second test is what holds it there.

## Phases

**Phase 1 - the red test.** Two tests in `ProductImageTests`: a deleted product leaves nothing in
the store, and a failing store does not prevent the delete. Run them and record that the first one
fails on today's code.

**Phase 2 - the fix.** `DeleteProductCommandHandler` takes `IProductImageStore`, reads the key
beside the variant ids, and after `SaveChangesAsync` deletes it inside a try/catch that logs a
warning.

**Phase 3 - the evidence.** Delete the two orphans from the running volume by hand, and show the
directory before and after.

**Phase 4 - say so.** The specs/019 paragraph in CLAUDE.md is accurate about replacement and silent
about deletion; one sentence fixes that. The sweeper gets its own issue.

## Verification

- `DB_PASSWORD=... dotnet test` - Catalog is 109 tests before this, 111 after.
- The new test **must** be seen failing before the fix. A green test written after the code is a
  test of the code rather than of the defect.
- Bruno's product-deletion request still passes.
- On the running stack: upload, delete, list the volume.
