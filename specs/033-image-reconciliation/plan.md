# Implementation Plan: Finding the images nobody can name

> Completed on 2026-09-27, after the feature merged (#74), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Branch**: `033-image-reconciliation` | **Spec**: [spec.md](spec.md) | **Closes**: #67

## Summary

Let an administrator ask what the image store holds that no product or variant row can name, and
separately reclaim it. `IProductImageStore` learns to list itself; a scan reads every live key
**first** (a failure is fatal), lists the store, and keeps what is neither live nor younger than
`ProductImages:OrphanGraceHours`. `GET /api/products/images/orphans` reports and changes nothing;
`DELETE` on the same address reconciles again and removes what it finds. Nothing runs on a timer.
Decisions in [research.md](research.md).

## Technical Context

One new capability on `IProductImageStore`, one query that reconciles, one command that reclaims,
two Admin routes, and a configurable grace period.

**No migration. No new table. No new message. No client change.**

**Language/Version**: C# / .NET 10

**Primary Dependencies**: MediatR, `Microsoft.Extensions.Options` (`OrphanImageOptions`), EF Core with
Npgsql for the live-key read

**Storage**: reads `products` and `product_variants` (`ImageContentType`, `ImageUpdatedAt`) in
`ecommerce_catalog_db`; lists and deletes in the image store (`FileSystemProductImageStore` at the time)

**Testing**: xUnit against real PostgreSQL (`OrphanImageTests`, 8 tests) with the tests' `TestImageStore`;
Bruno (a report and a seller refusal); the running stack

**Target Platform**: Catalog on 5057, through the gateway on 5000

**Constraints**: the live keys are read before the store is listed and a failure propagates; the
reclaim takes no key list; nothing on a schedule

**Scale/Scope**: a store in the tens of files at the time (17 scanned on the running stack); the
listing streams so a paged object store can implement it later

## Constitution Check

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0.

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | **Pass.** Untouched. Catalog reconciles its own store against its own rows. |
| II - Clean Architecture | **Pass.** Listing is added to the Application-layer interface; the handler never sees a directory. |
| III - Atomic writes, idempotent messaging | **Pass.** Nothing is written and nothing is published. The delete is idempotent by construction - a key already gone is not an error, which `DeleteAsync` already promises. |
| IV - Identity from the token | **Pass.** Admin only, decided by the attribute. There is no ownership to check: an orphan's row is gone, so nothing can say whose it was. |
| V - Evidence over assumption | **Pass.** Every dangerous case is shown **red before its guard exists**: a live product image, a live variant image, a file written seconds ago, and a failing database read. |

No Complexity Tracking entries.

**Post-design re-check**: unchanged. The one design change made while building - `ILiveImageKeys`, a
one-method interface the repository implements (tasks T024) - narrows a dependency and strengthens
Principle II rather than bending it.

## Project Structure

### Documentation (this feature)

```text
specs/033-image-reconciliation/
├── spec.md
├── plan.md                  # this file
├── research.md              # D1-D8
├── data-model.md            # no schema change; what is read and listed
├── quickstart.md
├── contracts/
│   └── api.md               # two Admin routes and the store's ListAsync
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source code touched (from the pull request)

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Application/
│   ├── Common/Interfaces/IProductImageStore.cs        # ListAsync, StoredImage
│   ├── Common/Interfaces/IProductRepository.cs        # ILiveImageKeys, implemented by the repository
│   ├── DependencyInjection.cs                         # OrphanImageScan, scoped
│   └── Products/Images/OrphanImages.cs                # query, command, scan, report, options
├── Ecommerce.Catalog.Infrastructure/
│   ├── DependencyInjection.cs                         # ILiveImageKeys forwarded to the repository
│   ├── Images/FileSystemProductImageStore.cs          # ListAsync, dotfiles excluded
│   └── Persistence/Repositories/ProductRepository.cs  # GetLiveImageKeysAsync
└── Ecommerce.Catalog.WebApi/
    ├── Controllers/ProductsController.cs              # GET/DELETE images/orphans, Admin
    ├── Program.cs                                     # Configure<OrphanImageOptions>
    └── appsettings.json                               # ProductImages:OrphanGraceHours = 24
server/tests/Ecommerce.Catalog.Tests/{OrphanImageTests.cs,CatalogTestFixture.cs}
bruno/product/orphan images report.yml
bruno/security-checks/a seller cannot read the orphan report.yml
CLAUDE.md
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why it is needed | Simpler alternative rejected because |
| :-- | :-- | :-- |

## The one defect that matters more than the feature

⚠️ **An empty live-key set must never mean "everything is an orphan".**

If the repository read throws and the code carries on with nothing, every file in the store becomes
a candidate and the delete removes the entire catalogue's images. Nothing about that is
recoverable.

So the live keys are read **first**, a failure propagates, and a test asserts that a failing
repository yields nothing reported and nothing deleted. That test is written before the reconciler
is, and it is the reason the reconciler reads in that order rather than the other.

Two more that are nearly as bad, both guarded and both tested red first:

- **A file written seconds ago is not an orphan.** An upload between its two steps has written bytes
  no row names *yet*.
- **The delete must not trust the caller's key list.** It re-reconciles and removes what it finds;
  the report is advice, not an instruction.

## Phases

**Phase 1 - the store can be listed.** `ListAsync` streaming `StoredImage`, excluding the store's
own dotfiles. A test that it finds what was saved and ignores a probe file.

**Phase 2 - the live keys.** A repository read returning every key a product or variant currently
names. This is the set everything else is compared against, so it is tested for both kinds of key.

**Phase 3 - the reconciler, dangerous cases first.** Four tests seen red, then
`FindOrphanImagesQuery`.

**Phase 4 - reclaiming.** `RemoveOrphanImagesCommand`, re-reconciling rather than taking keys.

**Phase 5 - the routes.** Two, `Admin` only. A test that a seller is refused.

**Phase 6 - end to end.** An orphan made the way the deliberate swallow makes one, found, and
removed; `verify-saga.sh`; Bruno.

**Phase 7 - say so.** CLAUDE.md, and the gotcha about what the one-instance assumption now costs.

## Verification

- Catalog 122 to about 132.
- The four dangerous-case tests **must be seen failing** before their guards.
- On the running stack: create an orphan, report it, reclaim it, and confirm the fourteen real
  camera images are untouched throughout.
- `verify-saga.sh` and Bruno.

**What the pull request recorded** (#74): Catalog 122 → **130**; six of the eight tests seen failing
against a naive reconciler before the guards, the seventh (the failing read) passing by construction;
Bruno 96/96 requests, 152/152 tests; `verify-saga.sh` passed. On the running stack: `scanned 17,
liveKeys 16`, one orphan (`17B`, `72h old`) named with the one-instance note; `DELETE` removed exactly
one with nothing failed; afterwards `scanned 16, liveKeys 16`. A seller asking or reclaiming → 403,
anonymous → 401, the fourteen real camera images untouched. The "a seller is refused" check of Phase 5
is the Bruno security check and the manual run, not an xUnit test.

## What this feature does not finish

- The one-instance assumption is named, not solved; specs/079 later made the store a shared S3 bucket
  and drew the `note` from `IProductImageStore.SharedAcrossInstances`.
- Nothing repairs a row that names a missing file; that is logged at Error where it can happen.
- No storefront screen; the report is read through the API.
