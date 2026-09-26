# Feature Specification: Product images in object storage

> Completed on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md (with docs/infrastructure/running-in-containers.md).

**Feature Branch**: `079-object-storage` | **Created**: 2026-09-26 | **Issue**: #114 (closes it)

**Status**: Merged (PR #163, 2026-09-26)

**Input**: Issue #114, "product images only work with one Catalog instance": an object-storage
`IProductImageStore` (S3-compatible; the issue named MinIO for development) behind the existing seam, a one-off copy
of the volume into the bucket, and an orphan reconciliation that lists the bucket. Acceptance: two Catalog instances
serve every image, and the orphan report finds nothing that a row names.

## Why

Product images live in a directory on the `catalog_images` volume (specs/019). A second Catalog instance would have
its own directory, so every image the first one stored would be a 404 there. Worse, the orphan reclaim (specs/033)
on the second instance would report the first one's images as orphans, and a person pressing *remove* would delete
them.

That second consequence is what made this more than a scaling note. [specs/033 D8](../033-image-reconciliation/research.md)
recorded the one-instance assumption as inherited rather than solved, and the report carried a `note` warning about
it. With a directory per instance the reclaim - the one part of the image feature whose failure mode is destroying
data somebody is using - becomes destructive the moment anybody runs two containers. The issue had been recorded as
an assumption since specs/019 and was found again on 2026-09-24.

## User Scenarios & Testing *(mandatory)*

### US1 - Every instance serves every image (P1)

Catalog stores images in an S3-compatible bucket. An image uploaded through one instance is served by any other,
and a replacement or a deletion through one is seen by all.

**Why this priority**: It is the issue's acceptance, and without it a second instance is a storefront with broken
pictures. Every other story follows from the images being in one place.

**Independent Test**: Run two Catalog instances against one bucket. Upload an image through the first, read it
through the second, delete it through the second, and read it again through the first.

**Acceptance Scenarios**:

1. **Given** two Catalog instances configured with the same bucket, **When** an image is uploaded through the first,
   **Then** the second serves the same bytes.
2. **Given** an image both instances can serve, **When** its product is deleted through the second instance,
   **Then** the image is a 404 on the first.
3. **Given** a key the bucket already holds, **When** a second write under that key arrives (from either
   instance), **Then** it is refused and the stored bytes stay as they were - the same write-once behaviour the
   directory store's `File.Move(overwrite: false)` gives.

---

### US2 - The orphan report lists the bucket (P1)

The reconciliation lists the bucket. It finds nothing that a row names, whichever instance runs it, and its note no
longer warns about one instance when the store is shared.

**Why this priority**: The reclaim deletes. A report that differs by instance is a report that deletes live images
from one of them, which is the defect the issue exists for.

**Independent Test**: Store two live images and one orphan through two different instances, run the report from
each, and compare.

**Acceptance Scenarios**:

1. **Given** two live images and one image no row names, stored through different instances, **When** the
   reconciliation runs on either instance, **Then** it reports the same one orphan, 3 scanned and 2 live.
2. **Given** the bucket store, **When** the report is read, **Then** its `note` says the store is shared object
   storage; **given** the directory store, **Then** it still carries the one-instance warning.
3. **Given** a bucket holding more keys than one listing page, **When** the store is listed, **Then** every key is
   returned, a page at a time, and the store's own dot-prefixed bookkeeping (the write probe) is left out.

---

### US3 - Nothing already uploaded is lost (P1)

The images already on the volume are copied into the bucket.
- It is idempotent: a key already in the bucket is left alone.
- It is safe on several instances at once.
- It runs at startup while the old volume is still mounted, and reports what it copied.

**Why this priority**: Moving storage without moving what is stored would blank every existing product picture on
the first start. It is as necessary as the new store.

**Independent Test**: Start Catalog on the bucket with the old volume mounted read-only and `ProductImages:ImportFrom`
set; read the import's log line; start it again and read the second line.

**Acceptance Scenarios**:

1. **Given** a directory holding three images, one of which the bucket already has, **When** the import runs,
   **Then** it copies two and counts one as already there; **When** it runs again, **Then** it copies none and
   counts three as already there.
2. **Given** several instances starting at once, each importing the same directory, **When** they finish, **Then**
   every image was copied exactly once in total and none of them reports a failure.
3. **Given** any import, **When** it finishes, **Then** the directory holds exactly what it held before - the import
   never deletes.

---

### US4 - A store that cannot be used stops the service at startup (P2)

An operator who starts Catalog with the bucket unreachable, a wrong secret or a missing setting is told at startup,
in words that name the problem, rather than meeting a 500 at the first upload.

**Why this priority**: It is the constitution's configuration rule (a required secret missing fails at startup) and
the same guarantee the directory store already gave (specs/019 D8). It is second only because the three stories
above are the feature; this one keeps it from failing quietly.

**Independent Test**: Construct the store with a wrong secret and with settings missing, and ask it to get ready.

**Acceptance Scenarios**:

1. **Given** a wrong secret, **When** the store gets ready, **Then** it throws an error naming the bucket and the
   server, after its patience runs out.
2. **Given** settings with no `Bucket` and no `SecretKey`, **When** the store is constructed, **Then** it throws an
   error listing `ProductImages:S3:Bucket is not set.` and `ProductImages:S3:SecretKey is not set.`
3. **Given** the S3 server still starting, **When** Catalog starts, **Then** it keeps trying for up to 30 seconds
   before giving up.

---

### Edge Cases

- **Two instances importing at once.** Both see a key missing and both try to store it. The second meets
  `If-None-Match: *`, gets a 412, and counts it as already there rather than as a failure.
- **A key the directory lists but no longer holds by the time it is read.** Skipped; there is nothing to copy.
- **A leftover write probe in the bucket** (a crash between the probe's put and delete). It is dot-prefixed, so the
  listing skips it and the orphan report never offers it for deletion.
- **A key that is not an image key** (`../escape-1.png`, `0199aa11-1.svg`, `Upper-1.png`, `folder/0199aa11-1.png`).
  Refused with `ArgumentException` before any request is made, by the same check the directory store uses.
- **A missing key.** A read returns `null` (a 404 from S3); a delete succeeds, as S3 answers it with success.
- **The old volume mounted read-only.** The directory store normally writes a probe at construction; the import
  opens it with the probe turned off, because a write there fails by design.
- **`ProductImages:ImportFrom` set to a directory that does not exist.** The import does not run.
- **An unknown `ProductImages:Store` value.** Startup throws, naming the value.

## Requirements *(mandatory)*

### Functional Requirements

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
  > Corrected on 2026-09-27: the code (`S3ImageStoreOptions.Problems`) requires `ServiceUrl`, `Bucket`, `AccessKey`
  > and `SecretKey`. `Region` is not required - it defaults to `us-east-1`, which a self-hosted server accepts.
  > A sixth setting, `PageSize` (default 1000), must be from 1 to 1000 or the service refuses to start.
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
- **FR-008** The import MUST never delete or change the directory it copies from, so abandoning the move halfway, or
  going back to the directory store, loses nothing that was on the volume.
- **FR-009** The import MUST log, at every start, how many images it copied, how many were already there and how many
  failed, and one warning per failure naming the key and the reason.
- **FR-010** A read MUST give the S3 connection back before the caller reads the bytes: the object is copied into
  memory (an image is at most `ProductImageKey.MaxBytes`, 2 MB), rather than holding a connection open for as long as
  a slow client takes.
- **FR-011** `SharedAcrossInstances` MUST default to `false` on the interface, so any store that does not say
  otherwise is reported as one instance's own - the warning errs on the side of the destructive case.
- **FR-012** No HTTP endpoint changes its address, its authorization or its response shape. The only visible change
  is the text of the orphan report's `note` when the store is shared; Bruno's orphan-report test accepts either note.

### Key Entities

- **Image store**: where product and variant image bytes live, behind `IProductImageStore`. Two implementations:
  a directory (one instance) and an S3-compatible bucket (every instance). It says whether it is shared.
- **Bucket**: `product-images` in compose; holds one object per image key, plus, transiently, a write probe.
- **Image key**: `{productId:N}-{ticks}.{ext}` or `variant-{variantId:N}-{ticks}.{ext}`, derived from the row
  (specs/019, 032). Unchanged by this feature; the bucket's object names are exactly these keys.
- **Legacy volume**: `catalog_images`, the directory from before this feature, now read-only and only ever read by the
  import.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With two Catalog containers over one bucket, an image uploaded through one is served byte-for-byte by the
  other, and the second instance serves every product image. Measured in the PR: **15/15**.
- **SC-002**: The orphan report run from each of the two instances finds **0 orphans** and says the store is shared.
  Measured in the PR: 15 scanned, 15 live, 0 orphans, from both.
- **SC-003**: A product deleted through one instance has its image become a 404 on the other. Measured in the PR.
- **SC-004**: The import copies every volume image once. Measured in the PR: **14** copied at the first start; at the
  second instance's start, 0 copied and 14 already there.
- **SC-005**: The store's guarantees are tested against a real S3 server in CI: 12 test cases in
  `S3ProductImageStoreTests`, `Ecommerce.Catalog.Tests` at **198/198**.
- **SC-006**: Every one of seven recorded mutations of the store or the import turns at least one of those tests red.
- **SC-007**: The whole Bruno collection passes through the rebuilt containers. Measured in the PR: **263/263
  requests, 428/428 tests**.

## Assumptions

- Any S3-compatible server that honours SigV4, path-style addressing and `If-None-Match: *` on `PutObject` will do.
  Only SeaweedFS 4.47 was exercised; real AWS S3 was not (see plan, "What this feature does not finish").
- Images are small (at most 2 MB), so reading one into memory is cheaper than holding an S3 connection.
- `dotnet run` and `start-dev` run one Catalog instance, so the directory store stays their default and needs no
  server.
- The move off the volume happens once; the `catalog_images` mount and `ProductImages__ImportFrom` stay in
  `docker-compose.app.yml` until someone removes them after the bucket has everything.
- The choice of SeaweedFS was made with the user (PR #163).

## Out of scope

- Serving images straight from the bucket, or through a CDN or presigned URLs. Catalog still streams them.
- Removing the directory store: it stays for `dotnet run` and for the tests that need to fail a write.
- Copying images back from the bucket to a directory (the import is one-way).
- Scheduling the orphan reclaim: it still runs only when an administrator asks (specs/033).
