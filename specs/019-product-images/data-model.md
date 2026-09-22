# Data Model: Product Images

## `products` (Catalog) - two nullable columns, expand only

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `ImageContentType` | `varchar(20)` | yes | `image/jpeg`, `image/png` or `image/webp`, as decided from the bytes |
| `ImageUpdatedAt` | `timestamptz` | yes | When the current image was set. Also its version, and part of its store key |

Constraints:

- `CK_products_image_complete`: `("ImageContentType" IS NULL) = ("ImageUpdatedAt" IS NULL)`
- `CK_products_image_type`: `"ImageContentType" IS NULL OR "ImageContentType" IN ('image/jpeg','image/png','image/webp')`

Neither column has a default, and existing rows get NULLs, meaning "no image". An earlier Catalog image
does not select these columns and runs unchanged.

## Store key (derived, not stored)

```text
{productId:N}-{ImageUpdatedAt.Ticks}.{jpg|png|webp}
```

## `ProductResponse` - one additive field

`ImageUrl: string?`, which is `/api/products/{id}/image?v={ticks}` or `null`.

## State transitions

```text
no image --PUT--> image(v1) --PUT--> image(v2) --DELETE--> no image
    ^                                                          |
    +------------------- DELETE (no-op) ----------------------+
```

Every arrow is one guarded `UPDATE` on the row. The file for a new version is written before its arrow
is taken, and the file for the old version is deleted only after.
