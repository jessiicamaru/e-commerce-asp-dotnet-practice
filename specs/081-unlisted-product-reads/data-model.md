# Data Model: What hangs on a product off the shelf

> Written on 2026-09-27, after the feature merged (#169), from the code at that merge, the pull request and
> docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

Two columns in `ecommerce_catalog_db`, one migration. No table is added and nothing is renamed or dropped.

---

## `products` - one column added

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `ImageAccessKey` | `uuid` | Nullable, no index | The unguessable part of the product image's address (`&k=`), new with every image. Null when the product has no image |

## `product_variants` - one column added

| Column | Type | Constraints | Meaning |
| :--- | :--- | :--- | :--- |
| `ImageAccessKey` | `uuid` | Nullable, no index | The same for a variant's own photograph (specs/032) |

No index: the key is never searched for. The image read loads the row by its primary key and compares.

## How the key is written

| Change | Statement | Key after |
| :--- | :--- | :--- |
| Upload or replace an image | `UPDATE ... SET "ImageContentType", "ImageUpdatedAt", "ImageAccessKey", "UpdatedAt" WHERE "Id" = @id AND "ImageUpdatedAt" = @seen` | A new `Guid.NewGuid()` |
| Remove an image | The same guarded statement with nulls | `NULL` |
| Anything else | - | Unchanged |

The guard on `ImageUpdatedAt` is specs/019's: of two replacements at once, one switches and the other is a 409.
The key rides in the same statement, so it can never name a different image than the row does.

## Who is served an image

| Product state | Request carries | Served? | `Cache-Control` |
| :--- | :--- | :--- | :--- |
| Listed (`ReviewStatus = 'Approved'`) | no key, or any key | Yes | `public, max-age=31536000, immutable` if `v` is current, else `no-cache` |
| Not listed (`Pending`, `Rejected`) | the image's own key | Yes | `private, no-cache` |
| Not listed | no key, or another key | No: 404 | - |

A variant image follows its product's listing and its own key.

## Migration `20260926150608_AddImageAccessKeys`

- `AddColumn<Guid>("ImageAccessKey", "products", "uuid", nullable: true)`
- `AddColumn<Guid>("ImageAccessKey", "product_variants", "uuid", nullable: true)`
- `UPDATE products SET "ImageAccessKey" = gen_random_uuid() WHERE "ImageUpdatedAt" IS NOT NULL;`
- `UPDATE product_variants SET "ImageAccessKey" = gen_random_uuid() WHERE "ImageUpdatedAt" IS NOT NULL;`
- `Down` drops both columns.

**Schema evolution**: additive only - two nullable columns and a backfill. An image built before this ignores the
columns and serves images as it did, which during a rollback means the old leak comes back, not an outage.

## What did not change

- `product_reviews` and `product_questions`: who may read them changed, not what they hold.
- `products.ReviewStatus` and `IsListed`: the rule is specs/045's, called, not changed.
- The bucket and the object keys (specs/079): the access key is part of the address only, never of the object name.
