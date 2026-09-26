# Data Model: One rule for "off the shelf"

**No table, column, index or migration changes** (FR-005).

## `products` (Catalog, `ecommerce_catalog_db`, 5433) - columns the rule reads

| Column | Type | Meaning |
| :-- | :-- | :-- |
| `ReviewStatus` | `varchar(20)`, default `'Approved'` | `Approved` / `Pending` / `Rejected` (specs/045) |
| `IsActive` | `boolean`, default `true` | `false` = withdrawn; no command writes it today |

## Derived, not stored

| Property | Definition | Used for |
| :-- | :-- | :-- |
| `Product.IsListed` | `ReviewStatus == Approved` | the review outcome; only `OnShelf` reads it |
| `Product.OnShelf` | `IsListed && IsActive` | every public read, shopper write, view, save, notice and sale |
| `ProductVariant.Sellable` | `IsActive && Product.OnShelf` | both pricing paths (checkout) |

## States

```text
                    IsActive = true              IsActive = false
  Approved     ──▶  ON THE SHELF                 off the shelf (withdrawn)   ← reads used to say "on"
  Pending      ──▶  off the shelf (waiting)      off the shelf
  Rejected     ──▶  off the shelf (rejected/taken down)  off the shelf
```

Off the shelf: 404 to the public for the product and what hangs on it; seen by its seller and staff.
