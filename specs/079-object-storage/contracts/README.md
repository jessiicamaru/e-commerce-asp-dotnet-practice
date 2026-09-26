# Contracts: Product images in object storage

> Written on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md (with docs/infrastructure/running-in-containers.md).

**Feature**: [spec.md](../spec.md) | **HTTP**: [http-api.md](./http-api.md)

This feature changed **no integration message, no gRPC service and no HTTP shape**. It relies on three interfaces
that are not external contracts of this system but that the feature cannot work without: Catalog's own image-store
seam, the S3 API, and the compose/environment contract that wires them together. They are written down here so that
replacing SeaweedFS, or adding a third store, starts from what is actually depended on.

---

## 1. `IProductImageStore` (Catalog Application, `Common/Interfaces/`)

The seam specs/019 built for object storage. Callers - the upload, removal, read and delete handlers, the orphan scan
and the import - did not change.

```csharp
public interface IProductImageStore
{
    Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    IAsyncEnumerable<StoredImage> ListAsync(CancellationToken ct = default);
    bool SharedAcrossInstances => false;   // new in specs/079
}

public readonly record struct StoredImage(string Key, long Size, DateTimeOffset LastModified);
```

| Member | What every implementation must do | Directory store | S3 store |
| :--- | :--- | :--- | :--- |
| `SaveAsync` | Store under a key that must not exist yet, all or nothing; an existing key throws `IOException` | temp file + `File.Move(overwrite: false)` | `PutObject` with `If-None-Match: *`; 412 becomes `IOException` |
| `OpenReadAsync` | The bytes, or `null` when nothing is there | `FileStream` | `GetObject` copied into memory; 404 is `null` |
| `DeleteAsync` | Remove; a missing key is not an error | `File.Delete` | `DeleteObject` (S3 answers success) |
| `ListAsync` | Stream everything, never the whole store in memory; leave out the store's own bookkeeping; `LastModified` is the store's timestamp | directory enumeration | `ListObjectsV2` pages; dot-prefixed keys skipped |
| `SharedAcrossInstances` | Whether every Catalog instance sees the same images | `false` (the default) | `true` |
| (key check) | Refuse anything that is not an image key before touching storage | `ProductImageKeys.EnsureSafe` | `ProductImageKeys.EnsureSafe` |

`S3ProductImageStore` also exposes `EnsureReadyAsync(TimeSpan patience)`, which `Program.cs` calls at startup; it is
not on the interface, because the directory store does the same check in its constructor.

---

## 2. The S3 API, as used

Through `AWSSDK.S3` 4.0.103.4, SigV4-signed, path-style (`ForcePathStyle = true`), region from settings (default
`us-east-1`).

| Operation | Used by | Behaviour relied on |
| :--- | :--- | :--- |
| `GetBucketLocation` | `EnsureReadyAsync` | 404 when the bucket does not exist |
| `PutBucket` | `EnsureReadyAsync` | Creates the bucket |
| `PutObject` (probe, then image) | `EnsureReadyAsync`, `SaveAsync` | **`If-None-Match: *` answered with 412 when the key exists** |
| `DeleteObject` | probe cleanup, `DeleteAsync` | Success for a missing key |
| `GetObject` | `OpenReadAsync` | 404 for a missing key |
| `ListObjectsV2` | `ListAsync` | `MaxKeys`, `ContinuationToken`, `IsTruncated`, `NextContinuationToken`; objects carry `Size` and `LastModified` |

The four behaviours checked against SeaweedFS before any code was written (research D1): SigV4 with a wrong secret
answered 403; a bucket created at startup; `If-None-Match` honoured with 412; a missing key deletable. A replacement
server must do the same.

---

## 3. Compose and environment

| Contract | Value |
| :--- | :--- |
| Service | `seaweedfs`, container `e-commerce-seaweedfs`, image `chrislusf/seaweedfs:4.47` |
| Command | `mini -dir=/data -bucket=product-images -master.telemetry=false -admin.ui=false` |
| Published port | `8333:8333` (S3 only; master, filer and admin stay inside) |
| Health | `wget -q -O /dev/null http://127.0.0.1:8333/healthz` |
| Identity | `AWS_ACCESS_KEY_ID` = `${SEAWEEDFS_ACCESS_KEY:?...}`, `AWS_SECRET_ACCESS_KEY` = `${SEAWEEDFS_SECRET_KEY:?...}` |
| Catalog container | `ProductImages__Store: S3`, `ProductImages__S3__ServiceUrl: http://seaweedfs:8333`, `ProductImages__S3__Bucket: product-images`, the two keys, `ProductImages__ImportFrom: /app/legacy/product-images`; depends on `seaweedfs` healthy |

The full list of settings is in [data-model.md](../data-model.md#configuration-that-stands-in-for-a-schema).

---

## Not changed

- **Messages**: none added or changed. The only message an image change causes is the audit entry
  (`AuditEntryRecorded`, specs/041), recorded by the upload and removal handlers exactly as before; the store switch
  adds none, and the import publishes nothing (it only logs).
- **gRPC**: `CatalogPricing` is untouched.
