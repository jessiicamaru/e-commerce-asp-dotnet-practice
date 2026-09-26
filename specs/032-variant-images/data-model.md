# Data Model: The picture follows the variant

> Written on 2026-09-27, after the feature merged (#73), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

One additive migration in `ecommerce_catalog_db`, and one new key form in the image store.

---

## `product_variants` — two columns and two checks

Migration `20260923055302_AddVariantImages`.

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `ImageContentType` | `character varying(20)` | yes | `image/jpeg`, `image/png` or `image/webp`, decided from the file's **bytes** at upload (`ImageFormat.Detect`); null when the variant has no photograph of its own, which is the normal case |
| `ImageUpdatedAt` | `timestamp with time zone` | yes | when the photograph was set - also its **version**, in its address (`?v=`) and in its store key |

Constraints, the same two `products` has had since specs/019:

| Name | SQL |
| :-- | :-- |
| `CK_product_variants_image_complete` | `("ImageContentType" IS NULL) = ("ImageUpdatedAt" IS NULL)` — a type and a version, or neither; half of one would give an address that serves nothing |
| `CK_product_variants_image_type` | `"ImageContentType" IS NULL OR "ImageContentType" IN ('image/jpeg', 'image/png', 'image/webp')` |

No index was added; the columns are read with the variant row they belong to.

**Schema compatibility**: additive - two nullable columns and two checks that every existing row
satisfies (both null). An image built before #73 does not select the columns and runs unchanged.
`Down` drops the checks, then the columns.

---

## The store key

| Whose | Key | Changed? |
| :-- | :-- | :-- |
| product | `{productId:N}-{version}.{ext}` | no — every existing file keeps resolving |
| variant | `variant-{variantId:N}-{version}.{ext}` | new (`ProductImageKey.ForVariant`) |

`{version}` comes from `ImageUpdatedAt` (`ProductImageKey.Version`), `{ext}` from the detected format.
The prefix is load-bearing: the first variant of every product reuses the product's id (12 of 12
products measured), so without it the two keys differ only when two timestamps happen not to agree.
`FileSystemProductImageStore` validates key shape before it becomes a path; its pattern became
`^(variant-)?[a-z0-9]+-[0-9]+\.(jpg|png|webp)$` — still no slash, no dot segment, no traversal.

Nothing about the image is stored anywhere else (FR-003): the key is derived from the row.

---

## Writes and their order

Every write is a guarded single statement on the version the handler read
(`IProductRepository.TrySetVariantImageAsync`):

```sql
UPDATE product_variants
SET "ImageContentType" = @type, "ImageUpdatedAt" = @now, "UpdatedAt" = now()
WHERE "Id" = @variantId AND "ImageUpdatedAt" IS NOT DISTINCT FROM @seen
```

(EF `ExecuteUpdateAsync`; shown as SQL for reading.) Zero rows means somebody else changed it
meanwhile: 409, and the new file is deleted.

| Operation | Order |
| :-- | :-- |
| upload / replace | write the new file under a new key → switch the row → delete the old file (logged and swallowed on failure) |
| remove | switch the row to null/null → delete the file (logged and swallowed) |
| delete the product | delete the row (and publish `ProductDeletedEvent`, unchanged) → delete the product's key and **every variant's key** (logged and swallowed) |

At every moment the row names a file that exists; a failure leaves at worst an orphan (which specs/033
then made findable).

## States

A variant photograph is either **absent** (both columns null → `ImageUrl` falls back to the product's,
or null if the product has none) or **present** (both set → `ImageUrl` is
`/api/products/{productId}/variants/{variantId}/image?v={version}`). There is no other state; the
first check makes one impossible.
