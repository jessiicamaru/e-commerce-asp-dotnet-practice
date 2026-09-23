# Contracts: Finding the images nobody can name

## Two endpoints, administrators only

| Method | Path | Effect |
| :-- | :-- | :-- |
| `GET` | `/api/products/images/orphans` | Reports. Changes nothing. |
| `DELETE` | `/api/products/images/orphans` | Removes what it finds, and answers with what it removed. |

`[Authorize(Roles = "Admin")]` on both — **not** `Seller,Admin`. A seller has no business deleting
files whose rows are gone, and an orphan cannot be attributed to a seller anyway: its row no longer
exists, so there is nothing left to say whose it was.

## What the report says

```json
{
  "graceHours": 24,
  "scanned": 17,
  "liveKeys": 15,
  "orphans": [
    { "key": "01a0c3fe668a7c3ea6a44a42fbd8cd00-639256405524921980.png",
      "bytes": 69, "lastModified": "2026-09-22T02:22:14Z", "ageHours": 31.2 }
  ],
  "orphanBytes": 69,
  "note": "One Catalog instance is assumed (specs/019). With two, each sees only its own directory and would report the other's images as orphans."
}
```

`scanned` and `liveKeys` are there so a nonsensical answer is visible as one: **`liveKeys: 0` with a
full store means the database read went wrong**, and the reader can see that without knowing the
implementation.

⚠️ The `note` is not decoration. It names the assumption that makes this endpoint destructive if
broken (research D8).

## What the delete says

The same shape, with `orphans` listing what was **removed** and a `failed` array for anything the
store refused:

```json
{ "graceHours": 24, "scanned": 17, "liveKeys": 15, "removed": [ … ], "removedBytes": 69, "failed": [] }
```

⚠️ **It does not take a list of keys.** A request naming keys would delete whatever it was told to,
and the caller's list is minutes old by the time a person has read the report and decided — long
enough for an upload to make one of those keys live. The delete performs its own reconciliation and
removes what it finds (research D7).

## The store gains one capability

```diff
 public interface IProductImageStore
 {
     Task SaveAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
     Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
     Task DeleteAsync(string key, CancellationToken cancellationToken = default);
+    IAsyncEnumerable<StoredImage> ListAsync(CancellationToken cancellationToken = default);
 }
+
+public readonly record struct StoredImage(string Key, long Size, DateTimeOffset LastModified);
```

**Streaming, not a list.** This interface is the seam object storage plugs into, and a bucket
listing is paged; a signature returning the whole thing invites an implementation that buys the
bucket into memory. The caller compares each key against a set and keeps only the differences.

**`LastModified` comes from the store, not from the key.** The ticks in a key are the *row's*
version, and an orphan has no row. The file's own timestamp is the only thing that can say how old
it is.

**The store excludes its own bookkeeping.** `FileSystemProductImageStore` writes
`.write-probe-{guid}` at construction; a crash at the wrong instant leaves one. It is not a product
image, and only the store knows that.

## Nothing runs on a schedule

No hosted service, no timer, no startup hook. The one piece of the image feature whose failure mode
is destroying data somebody is using does not get to act unattended (research D1).
