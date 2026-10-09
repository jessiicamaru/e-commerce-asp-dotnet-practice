# Data Model: A gallery of photographs per product

## New table `product_photos` (Catalog)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Id` | `uuid` | PK, application-generated (v7), `ValueGeneratedNever` |
| `ProductId` | `uuid` | FK → `products.Id`, **cascade** (a deleted product's rows go with it; its files are deleted by the handler after) |
| `Position` | `integer` | Order after the cover, 0-based; not unique (a reorder rewrites them all in one save) |
| `ContentType` | `varchar(32)` | `image/jpeg`, `image/png` or `image/webp`, from the bytes |
| `StorageKey` | `varchar(128)` | The file's key in the image store (research D2) - `photo-{id:N}-{ticks}.{ext}`, or a former cover's `{productId:N}-{ticks}.{ext}` |
| `AccessKey` | `uuid` | The address's key off the shelf (specs/081), new per row |
| `CreatedAt` | `timestamptz` | When it was added |

Indexes: `(ProductId, Position)`; unique `StorageKey`.

Migration `AddProductPhotos`: one new table, **expand-only** - nothing on `products` changes; an older image ignores it.

## Unchanged

- `products.ImageContentType` / `ImageUpdatedAt` / `ImageAccessKey`: the cover (specs/019, specs/081).
- `product_variants` image columns (specs/032).

## Operations and their states

| Operation | Rows | Files |
| :-- | :-- | :-- |
| Add, no cover | product's cover columns switched (guarded) | new cover key written first |
| Add, with cover | insert at `max(Position)+1` | `photo-` key written first |
| Remove a photograph | delete the row | its file deleted after the commit |
| Remove the cover, photographs left | cover columns switched to the first photograph's copy; its row deleted; positions closed up | first photograph's bytes copied to a new cover key first; old cover file and the first photograph's file deleted after |
| Remove the cover, none left | as today (columns cleared) | old cover file deleted after |
| Make cover | cover columns switched; chosen row deleted; old cover inserted at the chosen position with its own key | chosen bytes copied to a new cover key first; chosen file deleted after |
| Reorder | `Position` rewritten | none |
| Delete product | cascade | cover, variants' and every photograph's file deleted after |

Every change by a seller on an approved product also sets `ReviewStatus = Pending` (specs/045), in the same commit.

## Other declarations

- **Read cache** (specs/157): `product_photos` joins `CatalogueWrites`' table pattern.
- **Personal data** (specs/111): `product_photos` is declared not personal (a product's pictures, not a person's).
- **Orphan report** (specs/033): `GetLiveImageKeysAsync` adds every `StorageKey`.
