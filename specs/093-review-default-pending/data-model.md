# Data Model: A product inserted without a review status waits for review

## Migration `20260926201531_ReviewStatusDefaultsToPending` (Catalog)

| | SQL |
| :-- | :-- |
| Up | `ALTER TABLE products ALTER COLUMN "ReviewStatus" SET DEFAULT 'Pending';` |
| Down | `ALTER TABLE products ALTER COLUMN "ReviewStatus" SET DEFAULT 'Approved';` |

No column added, dropped, renamed or narrowed; no row rewritten; the EF model snapshot is unchanged (the model declares
no default - research D2). Expand/contract: nothing to contract; every image reads and writes the column as before.

## `products.ReviewStatus`

| Property | Value |
| :-- | :-- |
| Type | `character varying(20)`, not null |
| Values | `Approved`, `Pending`, `Rejected` (text, `HasConversion<string>()`) |
| Default | ~~`'Approved'`~~ → **`'Pending'`** |
| Written by the current code | always, explicitly: `Pending` for a seller, `Approved` for an administrator (specs/045) |
| Decided by the default | only an INSERT that omits the column - an image from before specs/045 |

## Who writes what

```text
  current image, seller          ──INSERT ... ReviewStatus='Pending'──▶  Pending   (unchanged)
  current image, administrator   ──INSERT ... ReviewStatus='Approved'─▶  Approved  (unchanged)
  pre-045 image, anybody         ──INSERT (no ReviewStatus)───────────▶  Approved → Pending   (#184)
```
