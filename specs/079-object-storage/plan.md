# Implementation Plan: Product images in object storage

> Completed on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md (with docs/infrastructure/running-in-containers.md).

**Branch**: `079-object-storage` | **Spec**: [spec.md](spec.md) | **Issue**: #114 | **Merged**: PR #163, 2026-09-26

## Summary

Move product and variant images from a directory on the `catalog_images` volume into an S3-compatible bucket that
every Catalog instance shares, behind the `IProductImageStore` seam specs/019 built for exactly this. A new
`S3ProductImageStore` (AWSSDK.S3, path-style) stores a key only when new (`If-None-Match: *`), reads into memory,
lists a page at a time, and checks the bucket at startup. The store says whether it is shared, and the orphan report
(specs/033) takes its `note` from that. `ProductImageImport` copies the old volume into the bucket at every start,
idempotently. Development and CI run SeaweedFS, because MinIO no longer publishes community images. No table, no
message and no endpoint shape changed. Reasoning and rejected alternatives are in [research.md](./research.md).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0

**Primary Dependencies**: `AWSSDK.S3` 4.0.103.4 (new, Catalog Infrastructure only); the existing MediatR,
MassTransit and EF Core stack is untouched

**Storage**: An S3-compatible bucket, `product-images` - SeaweedFS `chrislusf/seaweedfs:4.47` (`weed mini`) on host
port 8333 in compose, volume `seaweedfs_data`; the directory store (`ProductImages:Root`) under `dotnet run`. No
PostgreSQL change: `ecommerce_catalog_db` on 5433 is unchanged

**Testing**: xUnit, `S3ProductImageStoreTests` (12 test cases) against a real SeaweedFS, in a bucket of its own per
run; the rest of `Ecommerce.Catalog.Tests` against PostgreSQL on 5433 as before. Bruno through the gateway. A live
check with two Catalog containers over one bucket

**Target Platform**: Linux containers (Catalog on 8080 inside, 5057 on the host); Windows dev host with the directory
store

**Project Type**: Backend microservice change (Catalog, Infrastructure and WebApi layers), plus compose and CI

**Performance Goals**: None stated. An image is at most 2 MB (`ProductImageKey.MaxBytes`) and is read into memory

**Constraints**: A key is written once or not at all; the listing never holds the bucket in memory (specs/033); a
store that cannot be written stops the service at startup; the import never deletes; secrets come from `.env` and
compose refuses to start without them

**Scale/Scope**: Any number of Catalog containers over one bucket. The PR's live check used two instances, 15
product images, and 14 images on the old volume

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md) v1.1.0. The plan as merged had no
Constitution Check; this one was written on 2026-09-27 from the code at the merge.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The bucket is Catalog's alone, as the directory was: no other service reads or writes it, and images still reach the storefront only through Catalog's endpoints. It is shared between Catalog *instances*, not between services. SeaweedFS is infrastructure beside PostgreSQL and RabbitMQ, and no shared code was added to `Ecommerce.Contracts` or `Ecommerce.Shared` |
| **II. Clean Architecture Layering** | **Pass.** `IProductImageStore` stays in Application's `Common/Interfaces/`, gaining one default member; `OrphanImageScan` (Application) reads it without knowing the implementation. `S3ProductImageStore`, `ProductImageKeys` and `ProductImageImport` live in Infrastructure, and `AWSSDK.S3` is referenced only by the Infrastructure project. The store is chosen in Infrastructure's `DependencyInjection.cs`; WebApi's `Program.cs` only runs the startup check and the import, which is wiring |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** No entity change and no message is added. The store keeps the contract the write-switch-delete order depends on (specs/019 D3) - a key written once or not at all - through a conditional PUT (research D4), so nothing above it changed. The import is idempotent by key, and a repeated or concurrent copy affects nothing (a 412 counts as already there), which is this principle's "a repeated attempt affects zero rows" applied to objects |
| **IV. Identity Comes From the Token** | **Pass.** No endpoint, route or authorization attribute changed; the orphan report stays `Admin`. The S3 access key is the *service's* identity to the bucket, read from the environment (`SEAWEEDFS_ACCESS_KEY` / `SEAWEEDFS_SECRET_KEY`), never from a request and never in `appsettings.json` |
| **V. Evidence Over Assumption** | **Pass.** The four S3 behaviours the code relies on were checked against SeaweedFS before any code was written (research D1). The store's tests run against a real S3 server in CI, not a fake, because the guarantees are the server's (research D12). The issue's acceptance was verified live with two Catalog containers, and seven mutations each turned a test red. What was not exercised - real AWS S3, and a rollback past this change - is named below |

**Technology constraints**: *Configuration* - a missing S3 setting, an unreachable server or a wrong secret stops
the service at startup (research D5), and compose refuses to start without the two secrets (`${VAR:?...}`).
*Schema evolution* - no migration. *Service topology* - SeaweedFS publishes 8333, which is not a port natively
installed software commonly holds.

**Post-Phase 1 re-check**: no violations. The Complexity Tracking table below is empty.

## Design (Catalog)

- `Infrastructure/Images/ProductImageKeys.IsSafe`: the key-shape check, shared by both stores.
- `Infrastructure/Images/S3ProductImageStore`:
  - an `AmazonS3Client` with `ForcePathStyle` (SeaweedFS and most self-hosted S3 servers need it);
  - `SaveAsync` does `PutObject` with `IfNoneMatch = "*"`, and a 412 is an `IOException`, which is what the directory
    store's `File.Move(overwrite: false)` throws;
  - `OpenReadAsync` copies the object into memory. Images are at most `ProductImageKey.MaxBytes`, and disposing the
    response closes the connection.
  - `ListAsync` pages with `ContinuationToken`. `PageSize` is settable, so a test pages with 2.
  - `EnsureReadyAsync` creates the bucket if it is missing, then puts and deletes `.write-probe-<guid>`. It retries
    for up to 30 seconds, because at startup the S3 server may still be coming up.
- `IProductImageStore.SharedAcrossInstances` is false for the directory and true for S3. `OrphanImageScan.NoteFor`
  picks the report's note from it.
  > Corrected on 2026-09-27: the member that picks the note is the property `OrphanImageScan.Note`, not a method
  > `NoteFor`.
- `ProductImageImport.RunAsync(from, to)`: copies every key the directory holds and the bucket lacks.
  - "Already there" is a 412 from a concurrent import, and counts as skipped.
  - It returns what it copied, skipped and failed, and logs it.
- `DependencyInjection` chooses the store from `ProductImages:Store`. `Program.cs` resolves it at startup (as it
  does now) and runs the import when `ProductImages:ImportFrom` is set.
  > Completed on 2026-09-27: the import runs only when the resolved store is the S3 one **and** the `ImportFrom`
  > directory exists; the logging is done by `Program.cs` from the returned `ImageImportResult`, not by the import
  > itself. "Already there" also covers a key the bucket held before the copy was attempted (checked by a read
  > first), not only a 412.

## Compose and CI

- `docker-compose.yml` gets the `seaweedfs` service on port 8333, with the volume `seaweedfs_data` and a
  healthcheck.
- `docker-compose.app.yml`:
  - Catalog runs `ProductImages__Store: S3` against `http://seaweedfs:8333`, bucket `product-images`, and depends on
    `seaweedfs` being healthy.
  - The `catalog_images` volume moves to `/app/legacy`, read-only, with `ProductImages__ImportFrom`.
- `.env.example` gains the two SeaweedFS keys. `ci.yml` starts SeaweedFS in the build job with throwaway keys, the
  way it gives Postgres `postgres`.

Detail from the merged files: compose runs `weed mini` with `-bucket=product-images`, `-master.telemetry=false` and
`-admin.ui=false`; its healthcheck is `wget` on `/healthz`; `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` come from
`${SEAWEEDFS_ACCESS_KEY:?...}` / `${SEAWEEDFS_SECRET_KEY:?...}`. CI's step is a `docker run` of the same image
(without `-bucket`; the tests make their own) followed by up to 60 polls of `/healthz`, two seconds apart, and the
test step gains `SEAWEEDFS_ACCESS_KEY: ci-catalog` and `SEAWEEDFS_SECRET_KEY: ci-catalog-secret`. The smoke jobs
(`auth-smoke`, `saga-e2e`) run Catalog with `dotnet run` and so keep the directory store.

## Tests

`S3ProductImageStoreTests` run against the SeaweedFS on 8333, in a bucket of their own per run:
- a round trip;
- a missing key;
- a new key only;
- paging with a page of 2;
- the probe excluded;
- an unsafe key refused before any request;
- two stores (two instances) seeing each other's writes and deletes;
- the orphan scan over a shared bucket finding nothing a row names;
- the import copying what is missing, then nothing, and running concurrently without failing.

> Completed on 2026-09-27: the merged class also has
> `A_store_that_cannot_write_refuses_to_start_and_says_why` (a wrong secret, and missing settings named). With the
> unsafe-key theory's four cases that makes 12 test cases, the "12 new" in PR #163. Bruno's `orphan images report`
> test was changed to accept either store's note.

## Research

- **D1 - SeaweedFS over MinIO.** MinIO's images are gone from Docker Hub and quay.io. SeaweedFS is Apache-2.0, has
  one binary, and verified S3 behaviour: SigV4 (a wrong secret is 403), a bucket created at startup, `If-None-Match`
  honoured (412), and a missing key deletable. The user chose it.
- **D2 - the directory stays the default outside containers.** `dotnet run` and `start-dev` run one instance, and
  the directory needs no server. The containers are where two instances are possible.
- **D3 - the import at startup, not a script.** It reuses both stores' code, is idempotent by key, and is safe on
  several instances through `If-None-Match`. A script would be a third implementation of listing and copying.

These three, and nine more (D4-D12), are written out with their alternatives in [research.md](./research.md).

## Project Structure

### Documentation (this feature)

```text
specs/079-object-storage/
├── spec.md                  # Feature specification
├── plan.md                  # This file
├── research.md              # D1-D12, with rejected alternatives where recorded
├── data-model.md            # No table changed; the bucket, its keys and the settings that stand in for a schema
├── quickstart.md            # Validation scenarios: tests, the import, two instances, the orphan report
├── contracts/
│   ├── README.md            # The interfaces relied on: IProductImageStore, the S3 operations, compose and .env
│   └── http-api.md          # The image endpoints whose backing changed (no shape changed)
├── checklists/
│   └── requirements.md      # Spec quality checklist
└── tasks.md                 # Task list, all done
```

### Source Code (repository root), as merged

```text
server/src/Services/Catalog/
├── Ecommerce.Catalog.Application/
│   ├── Common/Interfaces/IProductImageStore.cs        # + SharedAcrossInstances (default false)
│   └── Products/Images/OrphanImages.cs                # + SharedNote, Note; report and reclaim use it
├── Ecommerce.Catalog.Infrastructure/
│   ├── Ecommerce.Catalog.Infrastructure.csproj        # + AWSSDK.S3 4.0.103.4
│   ├── DependencyInjection.cs                         # ProductImages:Store chooses S3 or FileSystem
│   └── Images/
│       ├── S3ProductImageStore.cs                     # new, with S3ImageStoreOptions
│       ├── ProductImageKeys.cs                        # new: the key-shape check, moved out of the directory store
│       ├── ProductImageImport.cs                      # new, with ImageImportResult
│       └── FileSystemProductImageStore.cs             # uses ProductImageKeys; probe can be turned off
└── Ecommerce.Catalog.WebApi/Program.cs                # EnsureReadyAsync(30 s), then the import when configured

server/tests/Ecommerce.Catalog.Tests/
├── S3ProductImageStoreTests.cs                        # new, 12 test cases against SeaweedFS
└── CatalogTestFixture.cs                              # the wrapping store forwards SharedAcrossInstances

server/docker-compose.yml                              # + seaweedfs service, seaweedfs_data volume
server/docker-compose.app.yml                          # Catalog on S3; catalog_images read-only at /app/legacy
server/.env.example                                    # + SEAWEEDFS_ACCESS_KEY, SEAWEEDFS_SECRET_KEY
.github/workflows/ci.yml                               # build job: start SeaweedFS, keys for the test step
bruno/product/orphan images report.yml                 # accepts either store's note
```

Documentation changed in the same PR: `CLAUDE.md`, `docs/features/catalog.md`,
`docs/infrastructure/running-in-containers.md`, `docs/infrastructure/database-setup.md`,
`docs/architecture/microservices-design.md`, `docs/guides/getting-started.md`, `docs/overview/project-overview.md`,
`docs/testing/testing-strategy.md`, `docs/project/timeline.md` and `docs/project/backlog.md` (#114 moved to Fixed).
Decision 60 in [decisions.md](../../docs/project/decisions.md) records the choice.

**Structure Decision**: Everything new sits beside `FileSystemProductImageStore` in Catalog's
`Infrastructure/Images/`, because it is a second implementation of the same seam. No new project, no new service.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Real S3 was not exercised.** Only SeaweedFS 4.47 was. The store uses standard operations (`PutObject` with
  `If-None-Match`, `GetObject`, `DeleteObject`, `ListObjectsV2`, `GetBucketLocation`, `PutBucket`), but whether a
  given production provider honours each the same way is unverified.
- **Catalog still streams every image itself.** No CDN, no presigned URL straight to the bucket (spec, out of scope).
- **The directory store still assumes one instance.** It remains `dotnet run`'s default, and its orphan report
  still carries the one-instance warning.
- **The move is not tidied away.** `ProductImages__ImportFrom` and the read-only `catalog_images` mount stay in
  `docker-compose.app.yml`; the compose comment says to drop both once the bucket has everything. The import runs,
  and finds nothing to copy, at every start until then.
- **A rollback past this change loses newer images.** The import is one-way. A Catalog image from before specs/079
  reads the directory, which lacks every image uploaded to the bucket since. This follows from the code; it was not
  tested, and no procedure for it is recorded.
- **Nothing backs up the bucket.** `seaweedfs_data` is one Docker volume, as each database's is.
