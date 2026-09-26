# HTTP Contract: Product images in object storage

> Written on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](../spec.md) | **Internal interfaces**: [README.md](./README.md)

**No endpoint was added, removed or reshaped.** Every image endpoint keeps its address, its authorization, its
request and its response shape; what changed is where the bytes behind them live. They are listed here because their
*backing* changed, so a reader checking this feature knows which requests exercise it. The full contracts are in
[specs/019](../../019-product-images/contracts/api.md), [specs/032](../../032-variant-images/contracts/api.md) and
[specs/033](../../033-image-reconciliation/contracts/api.md).

**Base**: Catalog, `http://localhost:5057`, through the gateway at `http://localhost:5000/api/products/...` (an
existing route; no gateway change).

Errors follow the project's RFC 7807 shape through `GlobalExceptionHandler`.

---

## Endpoints whose backing changed

| Method and path | Access | Store operation now | Unchanged behaviour |
| :--- | :--- | :--- | :--- |
| `PUT /api/products/{id}/image` | `Seller,Admin` | `PutObject` with `If-None-Match: *` under a new key, then the row switch, then `DeleteObject` of the old key | Multipart part `file`, JPEG/PNG/WebP by content, at most 2 MB; 200 with the product |
| `DELETE /api/products/{id}/image` | `Seller,Admin` | `DeleteObject` after the row is cleared | 204 |
| `GET /api/products/{id}/image?v=` | anonymous | `GetObject`, copied into memory | `Cache-Control: public, max-age=31536000, immutable` when `v` is current, else `no-cache`; `X-Content-Type-Options: nosniff`; 404 when there is no image |
| `PUT /api/products/{id}/variants/{variantId}/image` | `Seller,Admin` | as the product's, under a `variant-` key | 204 |
| `DELETE /api/products/{id}/variants/{variantId}/image` | `Seller,Admin` | `DeleteObject` | 204 |
| `GET /api/products/{id}/variants/{variantId}/image?v=` | anonymous | `GetObject` | as the product's; 404 when the variant has no photograph of its own |
| `DELETE /api/products/{id}` | `Seller,Admin` (owner) | `DeleteObject` of every image, after the row | 204 (specs/024, 029) |
| `GET /api/products/images/orphans` | `Admin` | `ListObjectsV2`, a page at a time | 200, report shape unchanged; **`note` text depends on the store** (below) |
| `DELETE /api/products/images/orphans` | `Admin` | `ListObjectsV2`, then `DeleteObject` per orphan | 200, same shape |

What the bucket adds, observable over HTTP: an image uploaded through one Catalog instance is served by every other,
and a deletion through one is a 404 on all (spec US1).

---

## `GET /api/products/images/orphans` - the one visible difference

The response shape is unchanged:

```json
{
  "graceHours": 24,
  "scanned": 15,
  "liveKeys": 15,
  "orphans": [],
  "orphanBytes": 0,
  "failed": [],
  "note": "The store is shared object storage (specs/079): every Catalog instance reads and writes the same bucket, so this report is the same whichever one answers it."
}
```

The numbers above are the PR's live check (15 scanned, 15 live, 0 orphans, from both instances). The `note` now comes
from the store's `SharedAcrossInstances`:

| Store | `note` |
| :--- | :--- |
| `S3` (the containers) | `The store is shared object storage (specs/079): every Catalog instance reads and writes the same bucket, so this report is the same whichever one answers it.` |
| `FileSystem` (`dotnet run`) | `One Catalog instance is assumed (specs/019). With two, each sees only its own directory and would report the other's images as orphans.` - unchanged |

The reclaim (`DELETE`) returns the same note. A client that matched the old text exactly would stop matching against
the containers; the one such client, Bruno's `orphan images report` test, was changed to accept either.

---

## Not changed

- No new endpoint, header, query parameter or status code.
- No gateway route or rate-limit policy.
- Nothing exposes the bucket: there is no presigned URL, and SeaweedFS's S3 port is for Catalog, not for browsers.
