# Phase 1 Data Model: Product images in object storage

> Written on 2026-09-27, after the feature merged (#163), from the code at that merge, the pull request and docs/features/catalog.md (with docs/infrastructure/running-in-containers.md).

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table changed, and there is no migration.** The feature moves image *bytes* from a directory to a bucket; the
rows that say which image is current were already store-agnostic. What stands in for a schema here is the bucket's
layout, the object keys, and the settings that choose and configure the store.

---

## What did not change, and why that matters

- **`products` and `product_variants`** keep `ImageContentType` and `ImageUpdatedAt` exactly as specs/019 and specs/032
  defined them, with their CHECK constraints. There is still **no file-name column**: the key is derived from the row
  (`ProductImageKey`), so the row decides which object is current and the store is never asked.
- **The key format is unchanged**, so an object in the bucket has the same name its file had in the directory. That
  is what lets the import copy by key with no mapping, and lets `ILiveImageKeys` (the orphan report's list of names
  the catalogue uses) work against either store without change.
- **No schema-evolution concern.** The constitution's expand-then-contract rule is about the database; nothing in it
  moved. The rollback concern is about *bytes* instead: an image from before specs/079 reads the directory, and the
  directory lacks what was uploaded to the bucket since (plan, "What this feature does not finish").

---

## The bucket

| Property | Value | Where it is set |
| :--- | :--- | :--- |
| Name | `product-images` | `ProductImages__S3__Bucket` in `docker-compose.app.yml`; also created by `weed mini -bucket=product-images` in `docker-compose.yml` |
| Server | SeaweedFS `chrislusf/seaweedfs:4.47`, `weed mini`, S3 on 8333 | `docker-compose.yml` |
| Persistence | Docker volume `seaweedfs_data`, mounted at `/data` | `docker-compose.yml` |
| Addressing | Path-style (`http://seaweedfs:8333/product-images/<key>`) | `ForcePathStyle = true` in `S3ProductImageStore` |
| Created by | SeaweedFS at its start, and by Catalog's `EnsureReadyAsync` if it is missing | research D5 |

### Objects

One object per image key. Nothing else is meant to live there.

| Object key | Example | Content type | Written by |
| :--- | :--- | :--- | :--- |
| `{productId:N}-{ImageUpdatedAt ticks}.{ext}` | `0199aa11...-638...png` | from the extension | a product image upload, or the import |
| `variant-{variantId:N}-{ticks}.{ext}` | `variant-0199aa11...-638...webp` | from the extension | a variant image upload, or the import |
| `.write-probe-<guid>` | `.write-probe-3f2a...` | none | `EnsureReadyAsync`, deleted straight after |

- `{ext}` is `jpg`, `png` or `webp`; the content type set on the object is `image/jpeg`, `image/png` or `image/webp`
  (`ProductImageKeys.ContentType`). It is for anybody reading the bucket directly - Catalog decides the type from the
  bytes and the row, never from the object's metadata.
- **The key-shape check** (`ProductImageKeys`): `^(variant-)?[a-z0-9]+-[0-9]+\.(jpg|png|webp)$`. No slash, no dot
  segment, no upper case. Checked before any request.
- **Dot-prefixed keys are the store's bookkeeping** and are skipped by the listing, so a probe a crash left behind is
  never counted as an image or offered as an orphan.
- **An object is written once.** `PutObject` carries `If-None-Match: *`; a second write under the same key gets 412 and
  the stored bytes stay (research D4). Replacing an image therefore always means a **new** key (new ticks), exactly as
  with the directory.

### Object lifecycle

```text
  upload / import ──PUT If-None-Match:*──▶ stored ──(row switched to it)──▶ current
                                              │                               │
                          row switch lost ────┘                               │ replaced, removed, product deleted
                          (orphan until reclaimed)                            ▼
                                                                         DELETE (missing is not an error)
```

The order - write, switch the row with a guarded `UPDATE`, then delete the old object - is specs/019 D3's and did not
change. An object that no row names is an orphan; the orphan report (specs/033) finds it by listing the bucket, and an
administrator reclaims it.

---

## The legacy volume

| Property | Value |
| :--- | :--- |
| Volume | `catalog_images` (`server_catalog_images` under compose's project prefix) |
| Mount, before specs/079 | `/app/data`, read-write; images under `/app/data/product-images` |
| Mount, since specs/079 | `/app/legacy`, **read-only** (`:ro`) |
| Read by | `ProductImageImport`, from `ProductImages__ImportFrom: /app/legacy/product-images`, through a `FileSystemProductImageStore` opened with `probe: false` |
| Written by | Nothing |

The import copies what the directory holds and the bucket lacks, at every start, and never deletes from the
directory. In the PR's live check it copied **14** images at the first start and reported 0 copied, 14 already there
at the second instance's start.

---

## Configuration that stands in for a schema

All under the `ProductImages` section. In compose they are environment variables with `__` for `:`.

| Setting | Type / values | Default | Required | Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `ProductImages:Store` | `FileSystem` \| `S3` (case-insensitive) | `FileSystem` | no | Which store. Anything else stops the service at startup, naming the value |
| `ProductImages:Root` | path | `data/product-images` | no | The directory, for `FileSystem` only (specs/019) |
| `ProductImages:S3:ServiceUrl` | URL | - | **yes** for `S3` | The S3 endpoint; `http://seaweedfs:8333` in compose |
| `ProductImages:S3:Bucket` | string | - | **yes** for `S3` | `product-images` in compose |
| `ProductImages:S3:AccessKey` | string | - | **yes** for `S3` | From `SEAWEEDFS_ACCESS_KEY` in compose |
| `ProductImages:S3:SecretKey` | string | - | **yes** for `S3` | From `SEAWEEDFS_SECRET_KEY` in compose |
| `ProductImages:S3:Region` | string | `us-east-1` | no | Signed into every request; a self-hosted server accepts any |
| `ProductImages:S3:PageSize` | int, 1-1000 | `1000` | no | Keys per listing page; out of range stops the service |
| `ProductImages:ImportFrom` | path | unset | no | A directory to import from at startup; only with `S3`, and only if the directory exists |
| `ProductImages:OrphanGraceHours` | int | `24` | no | Unchanged (specs/033) |

A missing required `S3` setting stops the service at construction, with every missing one listed, for example
`Product images cannot be stored in S3: ProductImages:S3:Bucket is not set. ProductImages:S3:SecretKey is not set.`

### Environment (`server/.env`)

| Variable | Used by | Required |
| :--- | :--- | :--- |
| `SEAWEEDFS_ACCESS_KEY` | SeaweedFS (`AWS_ACCESS_KEY_ID`), Catalog container, `S3ProductImageStoreTests` | yes - compose refuses to start without it |
| `SEAWEEDFS_SECRET_KEY` | SeaweedFS (`AWS_SECRET_ACCESS_KEY`), Catalog container, `S3ProductImageStoreTests` | yes - compose refuses to start without it |
| `S3_TEST_URL` | `S3ProductImageStoreTests` only | no - default `http://localhost:8333` |

The two keys can be any values; they only have to agree with each other (`.env.example`). CI uses the throwaway
`ci-catalog` / `ci-catalog-secret`.
