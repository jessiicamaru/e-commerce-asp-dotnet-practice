# Data Model: A deleted product takes its picture with it

> Written on 2026-09-27, after the feature merged (#68), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed.** #68 contains no migration: the fix is in
one handler, and everything it needs to find a product's file was already on the row. An earlier Catalog
image therefore runs against the schema unchanged.

---

## What "the image" is at this merge

There is no file-name column. A product's image is described entirely by two nullable columns on
`products`, added by specs/019:

| Column | Type | Meaning |
| :--- | :--- | :--- |
| `ImageContentType` | `character varying(20)`, nullable | `image/jpeg`, `image/png` or `image/webp` (CHECK `CK_products_image_type`) |
| `ImageUpdatedAt` | `timestamp with time zone`, nullable | When the current image was stored - its version |

CHECK `CK_products_image_complete`: both null or both set, never half an image.

The **store key** is derived from them by `ProductImageKey.For(product)`:

```text
{productId without dashes}-{ImageUpdatedAt ticks}.{jpg|png|webp}      e.g. 01a0cc478733768b8e8d127e7ce6dc6e-<ticks>.png
```

and the bytes live under that key in `IProductImageStore` - at this merge `FileSystemProductImageStore`,
a directory on the `catalog_images` volume (`/app/data/product-images` in the container). A product with no
image has no key, and the store is never asked.

---

## The order of a product deletion after this change

```text
1. load product (+ variants)
2. read variantIds and imageKey             <- before anything is removed (research D5)
3. stage removal of variants and product
4. publish ProductDeletedEvent              <- staged in the outbox
5. SaveChangesAsync                         <- ONE transaction: rows gone + outbox row
6. store.DeleteAsync(imageKey)              <- outside the transaction; a failure is logged and swallowed
```

| Failure at | Result |
| :--- | :--- |
| before 5 | Nothing changed: rows, event and file all as before |
| between 5 and 6 (crash) | Rows gone, event sent, file left - an **orphan**, accepted (research D2) |
| in 6 (store throws) | Same orphan, with a warning naming the key (research D3) |

The reverse order was rejected because its failure is a live row naming a missing file.

---

## State

A file is either named by a row (live) or not (an orphan). This feature removes the one path that made
orphans on every deletion; the remaining paths are the two failures above. Reclaiming orphans is specs/033.
