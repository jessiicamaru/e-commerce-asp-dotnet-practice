# Data Model: Finding the images nobody can name

> Written on 2026-09-27, after the feature merged (#74), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

**No table, column, index or migration changed.** The feature reads rows that already existed and lists
a store that already existed; an earlier Catalog image runs against the same schema unchanged.

---

## What is read — `ecommerce_catalog_db`

`ProductRepository.GetLiveImageKeysAsync` (through `ILiveImageKeys`) runs two no-tracking projections -
two reads rather than one join, because the two key shapes differ:

| Table | Columns | Filter | Key it produces |
| :-- | :-- | :-- | :-- |
| `products` | `Id`, `ImageUpdatedAt`, `ImageContentType` | both not null | `{Id:N}-{ticks}.{ext}` (`ProductImageKey.For`) |
| `product_variants` | `Id`, `ImageUpdatedAt`, `ImageContentType` | both not null | `variant-{Id:N}-{ticks}.{ext}` (`ProductImageKey.ForVariant`, specs/032) |

The result is a `HashSet<string>` with ordinal comparison. A type `ImageFormat` does not recognise
contributes no key (the database's `CK_*_image_type` checks make that unreachable in practice).

## What is listed — the image store

`IProductImageStore.ListAsync` streams one `StoredImage` per file:

| Field | Type | Source |
| :-- | :-- | :-- |
| `Key` | `string` | the file name |
| `Size` | `long` | the file's length |
| `LastModified` | `DateTimeOffset` | the file's own last-write time, **not** the ticks in its key (an orphan's row is gone) |

`FileSystemProductImageStore` enumerates its root directory, skips any name beginning with `.` (the
`.write-probe-{guid}` it writes at construction), and skips a file deleted between enumeration and
inspection.

## What is computed

```text
orphan  ⇔  key ∉ live keys  ∧  now − LastModified ≥ ProductImages:OrphanGraceHours
```

Returned as `OrphanImageReport(GraceHours, Scanned, LiveKeys, Orphans, OrphanBytes, Failed, Note)`;
each orphan is `OrphanImage(Key, Bytes, LastModified, AgeHours)`. Nothing is persisted: the report is
computed on each request.

## What is deleted

Only by `DELETE /api/products/images/orphans`, which recomputes the set above and calls
`IProductImageStore.DeleteAsync` for each key. A key the store refuses is logged and put in `Failed`; the
rest still go. No row is touched - an orphan has none.

## States

None. A file is live, young, or an orphan at the moment it is asked about, and the answer is not stored.
