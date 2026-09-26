---
description: "Task list for Finding the images nobody can name"
---

# Tasks: Finding the images nobody can name

> Completed on 2026-09-27, after the feature merged (#74), from the code at that merge, the pull request and
> docs/features/catalog.md. Story labels and paths were added to the existing tasks; T026 onward were
> added in this backfill.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

**Tests**: included, and written red first - this is the first feature whose failure mode is destroying
data somebody is using.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel; **[Story]**: US1 report, US2 reclaim, US3 refuse to guess

## Phase 1: The store can be listed

- [X] T001 [US1] `StoredImage` and `ListAsync` on IProductImageStore in server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductImageStore.cs
- [X] T002 [US1] Implement it in FileSystemProductImageStore, excluding the store's own dotfiles
- [X] T003 [P] [US1] Implement it in the tests' TestImageStore
- [X] T004 [US3] Test: it finds what was saved, and ignores a `.write-probe-` file

## Phase 2: The live keys

- [X] T005 [US1] `GetLiveImageKeysAsync` on IProductRepository, returning every key a product or variant currently names
- [X] T006 [US1] Test: both kinds of key appear; a row with no image contributes nothing

## Phase 3: The reconciler, dangerous cases first

- [X] T007 [US1] Test seen RED: a live product's image is never an orphan
- [X] T008 [US1] Test seen RED: a live variant's image is never an orphan
- [X] T009 [US1] Test seen RED: a file written seconds ago is never an orphan
- [X] T010 [US3] Test seen RED: a failing repository reports nothing rather than everything
- [X] T011 [US1] `FindOrphanImagesQuery`, reading the live keys FIRST (server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Images/OrphanImages.cs)
- [X] T012 [P] [US1] `ProductImages:OrphanGraceHours`, default 24, in Catalog's appsettings

## Phase 4: Reclaiming

- [X] T013 [US2] `RemoveOrphanImagesCommand`, re-reconciling rather than taking a key list
- [X] T014 [US2] Test: it removes an orphan and leaves a live image
- [X] T015 [US2] Test: a store failure on one key is reported in `failed` and does not stop the rest

## Phase 5: The routes

- [X] T016 [US1] [US2] `GET` and `DELETE /api/products/images/orphans`, `[Authorize(Roles = "Admin")]`
- [X] T017 [US1] The `note` naming the one-instance assumption
- [X] T018 [US1] Test: a seller is refused — *as built, a Bruno security check (`bruno/security-checks/a seller cannot read the orphan report.yml`) and the running-stack check (403, 403, 401), not an xUnit test; OrphanImageTests has no authorization case*

## Phase 6: End to end

- [X] T019 [US2] Create an orphan the way the deliberate swallow does, report it, reclaim it
- [X] T020 [US2] Confirm the fourteen real camera images are untouched throughout
- [X] T021 `verify-saga.sh`, Bruno

## Phase 7: Say so

- [X] T022 CLAUDE.md: the endpoints, the grace period, and what the one-instance assumption now costs
- [X] T023 PR closing #67

## Added while building

- [X] T024 `ILiveImageKeys`, a one-method interface the repository also implements. The scan needs
      one read, and a test for "the catalogue could not be read" should not have to implement
      twenty-one members to say so - the first attempt did, and that was the signal
- [X] T025 The red-first order had to be built differently in a compiled language: the reconciler
      was written NAIVE (every stored key an orphan), the tests run against it, six seen red, and
      the guards added one at a time. The seventh passed from the start, correctly - nothing
      catches the catalogue read, so it propagates by construction

## Added in the 2026-09-27 backfill (work the pull request shows)

- [X] T026 [US1] [US2] `OrphanImageScan`, the reconciliation both handlers share, registered scoped in Catalog's Application `DependencyInjection.cs`
- [X] T027 [US3] `ILiveImageKeys` forwarded to the `IProductRepository` instance in Catalog's Infrastructure `DependencyInjection.cs`, so both resolve to the same object in a scope
- [X] T028 [P] [US1] `Configure<OrphanImageOptions>` bound from the `ProductImages` section in server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs
- [X] T029 [P] [US1] Bruno `bruno/product/orphan images report.yml` - `liveKeys > 0` whenever `scanned > 0`, the note present, no `removed` field; the `DELETE` deliberately not in the collection
- [X] T030 [P] `TestImageStore` in server/tests/Ecommerce.Catalog.Tests/CatalogTestFixture.cs gains `ListAsync`
- [X] T031 The design record completed to the specs/001 standard: data-model.md, quickstart.md, plan structure, research labels and the contract's response-shape correction (2026-09-27)
- [X] T032 Merged as **#74** (`13e5d1c`) on 2026-09-23, closing #67: Catalog 130 tests; Bruno 96/96 requests, 152/152 tests; `verify-saga.sh` green; no client change
