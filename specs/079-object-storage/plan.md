# Implementation Plan: Product images in object storage

**Branch**: `079-object-storage` | **Spec**: [spec.md](spec.md) | **Issue**: #114

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
- `ProductImageImport.RunAsync(from, to)`: copies every key the directory holds and the bucket lacks.
  - "Already there" is a 412 from a concurrent import, and counts as skipped.
  - It returns what it copied, skipped and failed, and logs it.
- `DependencyInjection` chooses the store from `ProductImages:Store`. `Program.cs` resolves it at startup (as it
  does now) and runs the import when `ProductImages:ImportFrom` is set.

## Compose and CI

- `docker-compose.yml` gets the `seaweedfs` service on port 8333, with the volume `seaweedfs_data` and a
  healthcheck.
- `docker-compose.app.yml`:
  - Catalog runs `ProductImages__Store: S3` against `http://seaweedfs:8333`, bucket `product-images`, and depends on
    `seaweedfs` being healthy.
  - The `catalog_images` volume moves to `/app/legacy`, read-only, with `ProductImages__ImportFrom`.
- `.env.example` gains the two SeaweedFS keys. `ci.yml` starts SeaweedFS in the build job with throwaway keys, the
  way it gives Postgres `postgres`.

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

## Research

- **D1 - SeaweedFS over MinIO.** MinIO's images are gone from Docker Hub and quay.io. SeaweedFS is Apache-2.0, has
  one binary, and verified S3 behaviour: SigV4 (a wrong secret is 403), a bucket created at startup, `If-None-Match`
  honoured (412), and a missing key deletable. The user chose it.
- **D2 - the directory stays the default outside containers.** `dotnet run` and `start-dev` run one instance, and
  the directory needs no server. The containers are where two instances are possible.
- **D3 - the import at startup, not a script.** It reuses both stores' code, is idempotent by key, and is safe on
  several instances through `If-None-Match`. A script would be a third implementation of listing and copying.
