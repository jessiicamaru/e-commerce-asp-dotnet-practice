---
description: "Task list for Product images in object storage"
---

# Tasks: Product images in object storage

> Completed on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md (with docs/infrastructure/running-in-containers.md).

**Input**: Design documents from `/specs/079-object-storage/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included, and written first. The store's guarantees - a key stored once, a listing paged through, two
instances seeing one bucket - belong to the S3 server, so constitution Principle V requires them to be tested against
a real one (research D12).

## As recorded when the feature merged

- [X] T001 Tests first, in `S3ProductImageStoreTests`, against SeaweedFS: the round trip, a missing key, a new key
  only, paging, the probe, unsafe keys, two instances, the orphan scan over the shared bucket, and the import.
- [X] T002 `ProductImageKeys`, `S3ProductImageStore`, `SharedAcrossInstances`, the report's note,
  `ProductImageImport`, the store choice and its settings, and startup.
- [X] T003 SeaweedFS in compose, the Catalog container on S3 with the old volume imported, `.env.example`, and CI.
- [X] T004 A live check: two Catalog containers serve one image; the volume's images are in the bucket; the orphan
  report is clean; Bruno is green.
- [X] T005 Mutation checks, the docs (catalog, CLAUDE.md, infrastructure, counts, timeline, backlog), and the
  reference.

The five tasks above are kept as written. Below, the same work broken down to the standard of specs/001, with the
files each task touched; T006 onward re-state T001-T005 at that grain and add nothing that was not done.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 every instance serves every image, US2 the orphan report lists the bucket, US3 nothing uploaded is
  lost, US4 a store that cannot be used stops the service

---

## Phase 1: Setup

- [X] T006 [P] Verify SeaweedFS's S3 behaviour before writing code - SigV4 with a wrong secret is 403, a bucket is created at startup, `If-None-Match` answers 412, a missing key is deletable - after `minio/minio`, `quay.io/minio/minio` and `bitnami/minio` all failed to pull (research D1)
- [X] T007 [P] Add the `seaweedfs` service (`chrislusf/seaweedfs:4.47`, `mini -dir=/data -bucket=product-images -master.telemetry=false -admin.ui=false`, port 8333 only, `wget` healthcheck, identity from `${SEAWEEDFS_ACCESS_KEY:?}` / `${SEAWEEDFS_SECRET_KEY:?}`) and the `seaweedfs_data` volume to `server/docker-compose.yml`
- [X] T008 [P] Add `SEAWEEDFS_ACCESS_KEY` and `SEAWEEDFS_SECRET_KEY` to `server/.env.example`
- [X] T009 [P] Add the `Start SeaweedFS (S3)` step to the build job in `.github/workflows/ci.yml` (a `docker run` with throwaway keys `ci-catalog` / `ci-catalog-secret`, then up to 60 polls of `/healthz`), and the two keys to the `Test` step's environment - a step, because a service container cannot be given `weed mini`'s command
- [X] T010 Add `AWSSDK.S3` 4.0.103.4 to `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Ecommerce.Catalog.Infrastructure.csproj`

**Checkpoint**: `docker compose up -d` brings SeaweedFS up healthy on 8333; `dotnet build` succeeds.

---

## Phase 2: Foundational

- [X] T011 Move the key-shape regular expression out of `FileSystemProductImageStore` into `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Images/ProductImageKeys.cs` (`IsSafe`, `EnsureSafe`, `ContentType`), and call `EnsureSafe` from the directory store's `PathFor` (research D10)
- [X] T012 Add `bool SharedAcrossInstances => false` to `server/src/Services/Catalog/Ecommerce.Catalog.Application/Common/Interfaces/IProductImageStore.cs`, and forward it from the wrapping store in `server/tests/Ecommerce.Catalog.Tests/CatalogTestFixture.cs` (research D9)

---

## Phase 3: User Story 1 - Every instance serves every image (P1)

**Independent test**: two stores over one bucket see each other's writes and deletes (quickstart scenarios 1 and 3).

- [X] T013 [P] [US1] Write `What_is_saved_reads_back_and_is_gone_once_deleted`, `A_key_is_stored_once_and_a_second_write_is_refused`, `A_key_that_is_not_an_image_key_is_refused_before_any_request` (4 cases) and `Two_instances_see_each_others_images` in `server/tests/Ecommerce.Catalog.Tests/S3ProductImageStoreTests.cs`, each run in its own `catalog-tests-<guid>` bucket that `DisposeAsync` empties and deletes
- [X] T014 [US1] Implement `S3ImageStoreOptions` and `S3ProductImageStore` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Images/S3ProductImageStore.cs`: path-style `AmazonS3Client`; `SaveAsync` as `PutObject` with `IfNoneMatch = "*"` and a 412 rethrown as `IOException`; `OpenReadAsync` copied into memory with a 404 as `null`; `DeleteAsync` with a missing key not an error; `SharedAcrossInstances => true` (research D4, D6, D7)
- [X] T015 [US1] Choose the store from `ProductImages:Store` (`FileSystem` default, `S3`, anything else refused naming it) in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/DependencyInjection.cs`, binding `ProductImages:S3` (research D2)
- [X] T016 [US1] Point the Catalog container at the bucket in `server/docker-compose.app.yml` (`ProductImages__Store: S3`, `ServiceUrl`, `Bucket`, the two keys) and make it depend on `seaweedfs` being healthy

**Checkpoint**: an image stored through one store instance is read and deleted through another.

---

## Phase 4: User Story 2 - The orphan report lists the bucket (P1)

**Independent test**: the scan from either instance finds the same one orphan (quickstart scenario 4).

- [X] T017 [P] [US2] Write `The_listing_pages_through_everything_and_leaves_out_the_stores_own_bookkeeping` (page size 2, a leftover `.write-probe-left-behind`) and `The_orphan_report_from_either_instance_finds_only_what_no_row_names` in `server/tests/Ecommerce.Catalog.Tests/S3ProductImageStoreTests.cs`
- [X] T018 [US2] Implement `ListAsync` in `S3ProductImageStore.cs`: `ListObjectsV2` with `MaxKeys = PageSize` and `ContinuationToken`, yielded a page at a time, dot-prefixed keys skipped, `LastModified` from the object (research D8)
- [X] T019 [US2] Add `SharedNote` and the `Note` property to `OrphanImageScan` in `server/src/Services/Catalog/Ecommerce.Catalog.Application/Products/Images/OrphanImages.cs`, and return `_scan.Note` from both `FindOrphanImagesQuery` and `RemoveOrphanImagesCommand` (FR-007)
- [X] T020 [P] [US2] Let `bruno/product/orphan images report.yml` accept either store's note

**Checkpoint**: the orphan report from either instance agrees and says the store is shared.

---

## Phase 5: User Story 3 - Nothing already uploaded is lost (P1)

**Independent test**: the import copies what is missing, then nothing, and four at once copy each image once
(quickstart scenario 2).

- [X] T021 [P] [US3] Write `The_import_copies_what_the_bucket_lacks_and_nothing_twice` (2 copied and 1 there, then 0 and 3, the directory untouched) and `Imports_running_at_once_copy_each_image_once_and_fail_nothing` (four imports, six images) in `server/tests/Ecommerce.Catalog.Tests/S3ProductImageStoreTests.cs`
- [X] T022 [US3] Implement `ProductImageImport.RunAsync(from, to)` and `ImageImportResult` in `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Images/ProductImageImport.cs`: skip a key the bucket has, copy the rest, count an `IOException` on save as already there, collect failures with key and reason, never delete (research D3)
- [X] T023 [US3] Add a `probe` flag to the constructor of `server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Images/FileSystemProductImageStore.cs`, false only to read the read-only legacy volume (research D11)
- [X] T024 [US3] Run the import in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs` when the store is S3 and `ProductImages:ImportFrom` names an existing directory, logging copied / already there / failed and one warning per failure
- [X] T025 [US3] Mount `catalog_images` read-only at `/app/legacy` and set `ProductImages__ImportFrom: /app/legacy/product-images` in `server/docker-compose.app.yml`

**Checkpoint**: the first container start logs the volume's images copied; the next logs them already there.

---

## Phase 6: User Story 4 - A store that cannot be used stops the service (P2)

**Independent test**: a wrong secret and missing settings throw with the reason (quickstart scenario 5).

- [X] T026 [P] [US4] Write `A_store_that_cannot_write_refuses_to_start_and_says_why` in `server/tests/Ecommerce.Catalog.Tests/S3ProductImageStoreTests.cs`
- [X] T027 [US4] Implement `S3ImageStoreOptions.Problems()` (four required settings, `PageSize` 1-1000) checked in the constructor, and `EnsureReadyAsync(patience)` - bucket created if missing, probe put and deleted, retried every 2 seconds, then an error naming bucket and server - in `S3ProductImageStore.cs` (research D5)
- [X] T028 [US4] Call `EnsureReadyAsync(TimeSpan.FromSeconds(30))` on the resolved store at startup in `server/src/Services/Catalog/Ecommerce.Catalog.WebApi/Program.cs`, before the import

**Checkpoint**: `Ecommerce.Catalog.Tests` 198/198, 12 of them in `S3ProductImageStoreTests`.

---

## Phase 7: Verification, docs and merge

- [X] T029 Live check with two Catalog containers over one bucket: an image uploaded through the gateway (instance 1) served byte-for-byte by instance 2; 15/15 product images served by instance 2; the orphan report from both 15 scanned, 15 live, 0 orphans, with the shared note; a product deleted through instance 2 a 404 on instance 1; the import 14 copied, then 0 copied and 14 already there
- [X] T030 Bruno through the rebuilt containers: 263/263 requests, 428/428 tests
- [X] T031 Seven mutations, each turning `S3ProductImageStoreTests` red: no `If-None-Match`; the probe not skipped; only the first page listed; a missing key throwing; `SharedAcrossInstances` false; a concurrent 412 counted as a failure; any key accepted
- [X] T032 [P] Update `docs/features/catalog.md` (two stores, object storage, settings table, the tests row, known limits, the spec row) and `CLAUDE.md` (commands, test counts, service map, the images paragraph)
- [X] T033 [P] Update `docs/infrastructure/running-in-containers.md` (volumes, the SeaweedFS paragraph), `docs/infrastructure/database-setup.md`, `docs/architecture/microservices-design.md` and `docs/guides/getting-started.md`
- [X] T034 [P] Update the counts in `docs/overview/project-overview.md` and `docs/testing/testing-strategy.md`, add the row to `docs/project/timeline.md`, and move #114 to Fixed in `docs/project/backlog.md`
- [X] T035 Merge through PR #163, "feat(catalog): product images in an S3-compatible bucket every instance shares (SeaweedFS in dev and CI)", closing #114, on 2026-09-26

---

## Dependencies & Execution Order

- **Setup (T006-T010)** first; T006 decided which server the rest target.
- **Foundational (T011-T012)** before any store code: both stores call `ProductImageKeys`, and the note reads
  `SharedAcrossInstances`.
- **US1 (T013-T016)** before US2 and US3: the listing and the import both need a working store.
- **US2 (T017-T020)** and **US3 (T021-T025)** are independent of each other.
- **US4 (T026-T028)** needs the store's constructor (T014); T028 must come before T024 in `Program.cs`, because the
  import writes to the bucket the check makes ready.
- **Phase 7** after all stories.

Within each story the tests were written first and failed first (T001 as recorded).

## Notes

- 35 tasks: the five recorded at the merge, and 30 that break them down (5 setup, 2 foundational, 4 US1, 4 US2,
  5 US3, 3 US4, 7 verification and docs).
- The test-to-requirement map: FR-001 T013, T017; FR-002 and FR-003 T026; FR-007 T017; FR-008 T021; the import's
  concurrency T021.
- Where the docs' reference was regenerated (`python docs/tools/generate_reference.py`, named in T005 as "the
  reference"): nothing it generates - endpoints, messages, tables, gateway routes - changed, and whether it was run
  is not recorded.
