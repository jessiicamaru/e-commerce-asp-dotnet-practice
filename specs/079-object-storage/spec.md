# Feature Specification: Product images in object storage

**Feature Branch**: `079-object-storage` | **Created**: 2026-09-26 | **Issue**: #114 (closes it)

## Why

Product images live in a directory on the `catalog_images` volume (specs/019). A second Catalog instance would have
its own directory, so every image the first one stored would be a 404 there. Worse, the orphan reclaim (specs/033)
on the second instance would report the first one's images as orphans, and a person pressing *remove* would delete
them.

## User Scenarios

### US1 - Every instance serves every image (P1)

Catalog stores images in an S3-compatible bucket. An image uploaded through one instance is served by any other,
and a replacement or a deletion through one is seen by all.

### US2 - The orphan report lists the bucket (P1)

The reconciliation lists the bucket. It finds nothing that a row names, whichever instance runs it, and its note no
longer warns about one instance when the store is shared.

### US3 - Nothing already uploaded is lost (P1)

The images already on the volume are copied into the bucket.
- It is idempotent: a key already in the bucket is left alone.
- It is safe on several instances at once.
- It runs at startup while the old volume is still mounted, and reports what it copied.

## Requirements

- **FR-001** `S3ProductImageStore` behind `IProductImageStore` (AWSSDK.S3):
  - save only when the key is new (`If-None-Match: *`);
  - read, with a missing key as `null`;
  - delete, with a missing key not an error;
  - list, paged through `ListObjectsV2` and streamed, skipping the store's own dot-prefixed keys;
  - the same key-shape check as the directory store, before any request.
- **FR-002** Startup checks the bucket: it creates it if missing, then writes and deletes a probe. A store that
  cannot be written stops the service, as the directory store does.
- **FR-003** `ProductImages:Store` is `FileSystem` (the default) or `S3`. The `S3` settings are `ServiceUrl`,
  `Bucket`, `AccessKey`, `SecretKey` and `Region`. A missing S3 setting refuses to start, naming it.
- **FR-004** Development's S3 is **SeaweedFS** (`chrislusf/seaweedfs:4.47`, `weed mini`) in `docker-compose.yml`:
  - S3 is on host port 8333;
  - its credentials come from `.env` (`SEAWEEDFS_ACCESS_KEY` and `SEAWEEDFS_SECRET_KEY`, both required);
  - telemetry is off, and the admin UI is not published.
  - MinIO no longer publishes community images: `docker pull minio/minio` says the repository does not exist, and
    quay.io answers 401.
- **FR-005** The Catalog containers use `S3`, and import from the old volume, mounted read-only
  (`ProductImages:ImportFrom`).
- **FR-006** CI's build job starts SeaweedFS with throwaway credentials, so the store's tests run against a real S3
  server, as the rest run against a real PostgreSQL.
- **FR-007** The orphan report's note comes from the store: `IProductImageStore.SharedAcrossInstances`.

## Out of scope

- Serving images straight from the bucket, or through a CDN or presigned URLs. Catalog still streams them.
- Removing the directory store: it stays for `dotnet run` and for the tests that need to fail a write.
