# Phase 0 Research: Product images in object storage

> Written on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md (with docs/infrastructure/running-in-containers.md).

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

D1 to D3 were in `plan.md`'s "Research" section when the feature merged, and are kept there too. D4 onward are the
decisions the code, the pull request and its comments make without naming them as research. Where the record names
no rejected alternative, this file says "not recorded" rather than supplying one.

---

## D1 - SeaweedFS over MinIO

**Decision**: Development and CI run **SeaweedFS** (`chrislusf/seaweedfs:4.47`, in `weed mini` mode) as the
S3-compatible server. `weed mini` runs the master, volume server, filer and S3 gateway in one process, and only the S3
port, 8333, is published.

**Rationale**: MinIO's images are gone from Docker Hub and quay.io. SeaweedFS is Apache-2.0, has one binary, and
verified S3 behaviour: SigV4 (a wrong secret is 403), a bucket created at startup, `If-None-Match` honoured (412), and
a missing key deletable. The user chose it. Those four behaviours were checked against the server **before any code
was written** (PR #163), because each one is a guarantee the store's code relies on (D4, D5).

`weed mini` is a development shape: the compose comment says production points Catalog at real S3. Telemetry is
turned off (`-master.telemetry=false`) and the admin UI is disabled (`-admin.ui=false`).

**Alternatives considered**:

- **MinIO**, which the issue named. Rejected because it cannot be pulled: `docker pull minio/minio` says the
  repository does not exist, `quay.io/minio/minio` answers 401, and `bitnami/minio` is not found (PR #163).
- Other S3-compatible servers: not recorded.

---

## D2 - The directory stays the default outside containers

**Decision**: `ProductImages:Store` chooses the store: `FileSystem` (the default when unset) or `S3`. The containers
set `S3`; `dotnet run` and `start-dev` keep the directory. Any other value stops the service at startup, naming it.

**Rationale**: `dotnet run` and `start-dev` run one instance, and the directory needs no server. The containers are
where two instances are possible. The directory store also stays for the tests that need to fail a write
(`ProductImageTests` in specs/019), which is harder to arrange against a real bucket.

**Alternatives considered**:

- **S3 everywhere, the directory store removed.** Rejected (spec, out of scope): every `dotnet run` would then need
  a running S3 server, and the write-failure tests would lose their seam.

---

## D3 - The import at startup, not a script

**Decision**: `ProductImageImport.RunAsync(from, to)` copies every key the old directory holds and the bucket lacks.
`Program.cs` runs it at every start, when the store is `S3` and `ProductImages:ImportFrom` names a directory that
exists, and logs what it copied, what was already there and what failed.

**Rationale**: It reuses both stores' code, is idempotent by key, and is safe on several instances through
`If-None-Match`. A script would be a third implementation of listing and copying.

Running at *every* start is what idempotency buys: a restart copies nothing twice, so there is no "has the import
run yet?" state to keep. "Already there" is a key the bucket holds before the copy, or a 412 from a concurrent import
between the read and the write; both count as skipped, never as failed. The import **never deletes**: going back to
the directory store is setting one value, and nothing is lost if the move is abandoned halfway.

**Alternatives considered**:

- **A one-off script.** Rejected: a third implementation of listing and copying, beside the two stores.
- **Moving (copy then delete) rather than copying.** Not recorded as considered; the code's comment gives the reason
  it does not: the directory staying exactly as it was is what makes going back safe.

---

## D4 - A key is stored only when new: `If-None-Match: *`

**Decision**: `SaveAsync` is one `PutObject` with `IfNoneMatch = "*"`. A 412 (Precondition Failed) is rethrown as an
`IOException`, which is what the directory store's `File.Move(overwrite: false)` throws.

**Rationale**: `IProductImageStore.SaveAsync` promises to store "under a key that must not exist yet" and "either all
of it ... or nothing". The directory store keeps that with a temporary file and a non-overwriting move; one
conditional PUT keeps it for a bucket. Throwing the same exception type means nothing above the store changed: the
upload handlers' write-switch-delete order (specs/019 D3) and the import's "already there" branch behave the same on
either store. It is also where concurrent imports meet (D3), which is why the server's honouring of `If-None-Match`
was one of the four behaviours verified before code (D1).

Two mutations in the PR prove it is load-bearing: without `If-None-Match` the stored-once and concurrent-import tests
fail, and counting a 412 as a failure fails the concurrent-import test.

**Alternatives considered**:

- Not recorded. A check-then-put (read, then an unconditional PUT) is the obvious one; it leaves a window in which
  two writers both see the key missing, which is exactly what `Imports_running_at_once_copy_each_image_once_and_fail_nothing`
  runs four imports at once to exercise.

---

## D5 - The bucket is checked at startup, with patience

**Decision**: `S3ProductImageStore.EnsureReadyAsync(patience)` creates the bucket if `GetBucketLocation` says it is
missing, then puts and deletes `.write-probe-<guid>`. On an S3, HTTP or IO error it retries every 2 seconds until the
patience runs out (30 seconds from `Program.cs`), then throws an `InvalidOperationException` naming the bucket and the
server URL. The options are checked earlier still, in the constructor: every missing required setting is listed in
one error.

**Rationale**: A server that is unreachable, a wrong secret or a read-only identity must stop the service at startup
rather than fail the first upload - the constitution's configuration rule, and what the directory store already did
([specs/019 D8](../019-product-images/research.md)). Only a write proves the identity can write, which is why it is a
probe object and not just a bucket lookup. The patience exists because at startup the S3 server may still be coming
up; compose also makes Catalog wait for `seaweedfs` to be healthy, but `EnsureReadyAsync` does not rely on that.

The probe key starts with a dot, which the listing skips (D8), so a probe left behind by a crash is never offered as
an orphan.

**Alternatives considered**:

- **Check lazily, on the first upload.** Rejected by specs/019 D8 and the constitution: a 500 later instead of a
  refusal now.
- **Fail at the first error, without patience.** Not recorded as considered; the code comment gives the reason for
  retrying (the server may still be starting).

---

## D6 - Path-style addressing

**Decision**: The client is an `AmazonS3Client` with `ForcePathStyle = true` (`host/bucket/key`) and
`AuthenticationRegion` from `ProductImages:S3:Region`, default `us-east-1`.

**Rationale**: SeaweedFS and most self-hosted S3 servers need path-style addressing; AWS still accepts it. The region
is signed into every request: a self-hosted server accepts any, AWS wants the bucket's own, so it is a setting with a
default rather than a required one.

**Alternatives considered**: virtual-hosted-style addressing (`bucket.host/key`) - not recorded as considered; it
would need DNS for the bucket name on the compose network.

---

## D7 - A read copies the object into memory

**Decision**: `OpenReadAsync` copies the object's response stream into a `MemoryStream` and disposes the response
before returning. A 404 returns `null`.

**Rationale**: Images are at most `ProductImageKey.MaxBytes` (2 MB), and disposing the response closes the
connection. The response owns an S3 connection that must be given back rather than held for as long as a slow client
takes to read the image.

**Alternatives considered**: returning the response stream itself - not recorded as considered beyond the reason
above.

---

## D8 - The listing pages, streams, and skips the store's own keys

**Decision**: `ListAsync` pages through `ListObjectsV2` with `ContinuationToken`, `MaxKeys` = `PageSize` (default
1000, S3's own ceiling), yielding each object as its page arrives. Any key starting with `.` is skipped. `PageSize` is
settable so a test pages with 2.

**Rationale**: specs/033 made the listing an `IAsyncEnumerable` precisely so a bucket would never be held in memory
([specs/033 D4](../033-image-reconciliation/research.md)); this is the implementation that signature was waiting for.
Skipping dot-prefixed keys keeps the store's bookkeeping out of the orphan report, as the directory store already did
for its probe file. `LastModified` is the object's own timestamp, which is what the orphan grace period needs (an
orphan has no row to say how old it is).

Two mutations prove it: listing only the first page and not skipping the probe each fail the listing test.

**Alternatives considered**: none recorded.

---

## D9 - `SharedAcrossInstances` belongs to the store, and the note follows it

**Decision**: `IProductImageStore` gains `bool SharedAcrossInstances => false` as a default interface member. The
S3 store returns `true`. `OrphanImageScan.Note` returns `SharedNote` when the store is shared and the existing
`OneInstanceNote` otherwise, and both the report and the reclaim return that note.

**Rationale**: Only the store knows whether a second instance sees the same images. The report's warning existed
because of the directory ([specs/033 D8](../033-image-reconciliation/research.md)); it must stay for the directory and
go for the bucket, so the choice is read from the store rather than from configuration the report would have to
interpret. Defaulting to `false` means a store that does not say is reported as the destructive case. A default
member also leaves every other implementation compiling unchanged. The one that wraps another store - the test
fixture's, in `CatalogTestFixture.cs` - forwards it explicitly, or it would report `false` whatever it wrapped.

A mutation proves it: `SharedAcrossInstances` false fails the orphan-report and two-instances tests.

**Alternatives considered**: none recorded.

---

## D10 - One key-shape check, shared by both stores

**Decision**: The regular expression `^(variant-)?[a-z0-9]+-[0-9]+\.(jpg|png|webp)$` moves from
`FileSystemProductImageStore` into `ProductImageKeys.IsSafe` / `EnsureSafe`, which both stores call before a key
becomes a path or an object name. `ProductImageKeys.ContentType` gives the object a content type from its extension.

**Rationale**: Keys are generated by `ProductImageKey`, never taken from a request, but a store is where a string
meets the disk or the bucket, so it refuses anything else - no slash, no dot segment, no traversal. The `variant-`
prefix is kept because the first variant of a product reuses the product's id (specs/020, 032). The content type is
for anybody who reads the bucket directly; Catalog itself still decides the type from the bytes (specs/019 D4).

A mutation proves it: accepting any key fails the unsafe-key test.

**Alternatives considered**: a second copy of the pattern in the S3 store - not recorded as considered; two copies
of a security check drift.

---

## D11 - The old volume is mounted read-only, and read without a probe

**Decision**: `docker-compose.app.yml` mounts `catalog_images` at `/app/legacy` with `:ro`, and sets
`ProductImages__ImportFrom: /app/legacy/product-images`. The import opens it as
`FileSystemProductImageStore(importFrom, probe: false)`.

**Rationale**: Nothing writes to the old volume any more, and read-only says so to the operating system as well as to
the reader of the file. The directory store normally writes and deletes a probe at construction; on a read-only mount
that write would fail by design, so the constructor gained a `probe` flag, false only for this read.

**Alternatives considered**: none recorded.

---

## D12 - Tested against a real S3 server, in a bucket of its own per run

**Decision**: `S3ProductImageStoreTests` run against the SeaweedFS on 8333 (`S3_TEST_URL` to override), signed with
`SEAWEEDFS_ACCESS_KEY` / `SEAWEEDFS_SECRET_KEY`, each run in a bucket named `catalog-tests-<guid>` that it empties and
deletes afterwards. CI's build job starts SeaweedFS in a **step**, with throwaway keys (`ci-catalog` /
`ci-catalog-secret`), like the databases' `postgres`.

**Rationale**: The store's guarantees - a key stored only when new, a listing paged through, two instances seeing one
bucket - are the server's, as the other tests' guarantees are PostgreSQL's (constitution Principle V). A step rather
than a service container, because a service container cannot be given `weed mini`'s command. A test with a missing
key fails with a message saying which variable to set, rather than being skipped.

**Alternatives considered**:

- **A fake or in-memory S3.** Not recorded as considered by name; the CI comment gives the reason it would not do:
  the guarantees under test are the server's.
- **A GitHub Actions service container.** Rejected: it cannot be given `weed mini`'s command.

---

## Open risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| Only SeaweedFS was exercised | A production S3 that differs (for example in `If-None-Match` support or region handling) is untested | The store uses only standard S3 operations; the four behaviours it relies on are named in D1 and can be checked against another server the same way |
| The import is one-way | Rolling Catalog back to an image from before specs/079 reads the directory, which lacks every image uploaded since | Not mitigated; see plan, "What this feature does not finish" |
| `seaweedfs_data` is a single volume | Losing it loses every image uploaded since the move | The legacy volume is kept read-only, so images from before the move survive; nothing else backs the bucket up |
