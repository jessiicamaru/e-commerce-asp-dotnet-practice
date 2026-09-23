# Tasks: Finding the images nobody can name

## Phase 1: The store can be listed

- [ ] T001 `StoredImage` and `ListAsync` on IProductImageStore in server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductImageStore.cs
- [ ] T002 Implement it in FileSystemProductImageStore, excluding the store's own dotfiles
- [ ] T003 Implement it in the tests' TestImageStore
- [ ] T004 Test: it finds what was saved, and ignores a `.write-probe-` file

## Phase 2: The live keys

- [ ] T005 `GetLiveImageKeysAsync` on IProductRepository, returning every key a product or variant currently names
- [ ] T006 Test: both kinds of key appear; a row with no image contributes nothing

## Phase 3: The reconciler, dangerous cases first

- [ ] T007 Test seen RED: a live product's image is never an orphan
- [ ] T008 Test seen RED: a live variant's image is never an orphan
- [ ] T009 Test seen RED: a file written seconds ago is never an orphan
- [ ] T010 Test seen RED: a failing repository reports nothing rather than everything
- [ ] T011 `FindOrphanImagesQuery`, reading the live keys FIRST
- [ ] T012 `ProductImages:OrphanGraceHours`, default 24, in Catalog's appsettings

## Phase 4: Reclaiming

- [ ] T013 `RemoveOrphanImagesCommand`, re-reconciling rather than taking a key list
- [ ] T014 Test: it removes an orphan and leaves a live image
- [ ] T015 Test: a store failure on one key is reported in `failed` and does not stop the rest

## Phase 5: The routes

- [ ] T016 `GET` and `DELETE /api/products/images/orphans`, `[Authorize(Roles = "Admin")]`
- [ ] T017 The `note` naming the one-instance assumption
- [ ] T018 Test: a seller is refused

## Phase 6: End to end

- [ ] T019 Create an orphan the way the deliberate swallow does, report it, reclaim it
- [ ] T020 Confirm the fourteen real camera images are untouched throughout
- [ ] T021 `verify-saga.sh`, Bruno

## Phase 7: Say so

- [ ] T022 CLAUDE.md: the endpoints, the grace period, and what the one-instance assumption now costs
- [ ] T023 PR closing #67
