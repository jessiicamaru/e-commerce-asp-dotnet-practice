# Tasks: Finding the images nobody can name

## Phase 1: The store can be listed

- [X] T001 `StoredImage` and `ListAsync` on IProductImageStore in server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductImageStore.cs
- [X] T002 Implement it in FileSystemProductImageStore, excluding the store's own dotfiles
- [X] T003 Implement it in the tests' TestImageStore
- [X] T004 Test: it finds what was saved, and ignores a `.write-probe-` file

## Phase 2: The live keys

- [X] T005 `GetLiveImageKeysAsync` on IProductRepository, returning every key a product or variant currently names
- [X] T006 Test: both kinds of key appear; a row with no image contributes nothing

## Phase 3: The reconciler, dangerous cases first

- [X] T007 Test seen RED: a live product's image is never an orphan
- [X] T008 Test seen RED: a live variant's image is never an orphan
- [X] T009 Test seen RED: a file written seconds ago is never an orphan
- [X] T010 Test seen RED: a failing repository reports nothing rather than everything
- [X] T011 `FindOrphanImagesQuery`, reading the live keys FIRST
- [X] T012 `ProductImages:OrphanGraceHours`, default 24, in Catalog's appsettings

## Phase 4: Reclaiming

- [X] T013 `RemoveOrphanImagesCommand`, re-reconciling rather than taking a key list
- [X] T014 Test: it removes an orphan and leaves a live image
- [X] T015 Test: a store failure on one key is reported in `failed` and does not stop the rest

## Phase 5: The routes

- [X] T016 `GET` and `DELETE /api/products/images/orphans`, `[Authorize(Roles = "Admin")]`
- [X] T017 The `note` naming the one-instance assumption
- [X] T018 Test: a seller is refused

## Phase 6: End to end

- [X] T019 Create an orphan the way the deliberate swallow does, report it, reclaim it
- [X] T020 Confirm the fourteen real camera images are untouched throughout
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
