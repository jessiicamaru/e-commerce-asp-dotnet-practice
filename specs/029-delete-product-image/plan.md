# Implementation Plan: A deleted product takes its picture with it

> Completed on 2026-09-27, after the feature merged (#68), from the code at that merge, the pull request and docs/features/catalog.md.

**Branch**: `029-delete-product-image` | **Spec**: [spec.md](spec.md) | **Closes**: #66

## Summary

`DeleteProductCommandHandler` gains `IProductImageStore`. It reads `ProductImageKey.For(product)` beside
the variant ids, before the row is removed; stages the removal, publishes `ProductDeletedEvent` and saves
once, exactly as specs/024 left it; and only then, outside the transaction, deletes the key in a
`try`/`catch` that logs a warning and swallows the failure. Two tests in `ProductImageTests` hold the two
halves: the file goes, and a failing store does not stop the deletion.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: MediatR 12.4.1, MassTransit 8.3.6 (outbox, unchanged), `IProductImageStore`
(specs/019)

**Storage**: `ecommerce_catalog_db` (5433), unchanged; image bytes on the `catalog_images` volume through
`FileSystemProductImageStore`

**Testing**: xUnit against a real PostgreSQL, with the fixture's `TestImageStore`, which wraps the real
`FileSystemProductImageStore` (a temporary directory) and can be told to fail (`FailDeletes`)

**Target Platform**: Catalog (5057) behind the gateway (5000)

**Constraints**: The row and its event stay one transaction; the file may never be deleted before the row;
no store failure may fail a deletion; nothing may assume a POSIX filesystem (FR-005)

**Scale/Scope**: One handler

One handler, one dependency, one try/catch, two tests. `Ecommerce.Catalog.Tests` already has a
controllable store (`TestImageStore`) with `FailDeletes` and a `Files()` listing, so both scenarios
are expressible without new test infrastructure.

**No data model change. No contract change. No migration. No client change.**

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. The verdicts below were
written without an explicit Pass/Fail; each is a **Pass**, stated on 2026-09-27.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Untouched. Catalog deletes its own bytes; no new cross-service call. |
| II - Clean Architecture | **Pass.** The handler talks to `IProductImageStore`, an Application-layer interface. It must not touch a directory - FR-005. |
| III - Atomic writes, idempotent messaging | **Pass - the delicate one.** The file delete sits *outside* the transaction, deliberately: it is not part of the atomic write and must not be able to fail it (D2, D3). The row and the `ProductDeletedEvent` stay in one transaction exactly as today, and nothing about the outbox changes. A redelivery is unaffected - no consumer consults the store. |
| IV - Identity from the token | **Pass.** Unchanged. `SellerOwnership.RequireCanWrite` still runs before anything is removed. |
| V - Evidence over assumption | **Pass.** The test goes **red first** and is shown doing so. The claim that the replace path is already correct was measured on the running stack - three uploads, one file - not inferred from the code. |

**Post-design re-check** (2026-09-27, against the merged code): no violations. The file delete is outside
the atomic write by design and the row and event are still one `SaveChangesAsync`; a redelivered
`ProductDeletedEvent` still touches only Inventory's rows.

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

## Project Structure

### Documentation (this feature)

```text
specs/029-delete-product-image/
├── spec.md
├── plan.md               # This file
├── research.md           # D1-D6
├── data-model.md         # Added 2026-09-27: no table changed; what the image data is
├── contracts/api.md      # No contract change
├── quickstart.md
├── checklists/requirements.md
└── tasks.md
```

### Source Code (as changed by #68)

```text
server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Commands/DeleteProduct/DeleteProductCommand.cs   # + IProductImageStore, key read early, delete after save
server/tests/Ecommerce.Catalog.Tests/ProductImageTests.cs                                                          # + 2 tests
CLAUDE.md                                                                                                           # deletion in the specs/019 paragraph
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Orphans are still possible on purpose**: a swallowed store failure, or a crash between the save and
  the file delete. Nothing reconciles the directory against the table at this merge; that is #67
  (specs/033, which added an on-request report and reclaim, never on a timer).
- **An order page for a deleted product loses its picture** (research D1) - stated, not fixed.
- **One Catalog instance** is still assumed by the directory store (specs/019).

Since then: specs/032 extended the same handler to delete variant photographs, and specs/079 moved the
containers' store to an S3 bucket; the row-then-bytes order and the swallow are unchanged.
