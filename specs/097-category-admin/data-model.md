# Data Model: Administrators manage categories

**No table, column, index or migration changes.**

## `categories` (Catalog, `ecommerce_catalog_db`, 5433)

| Column | Written by this feature | Notes |
| :-- | :-- | :-- |
| `Name` | the rename | the default language's (Vietnamese) name, ≤ 100 |
| `Description` | the rename | ≤ 500, null when blank |
| `Slug` | never after creation | unique |
| `UpdatedAt` | the rename | |
| `IsActive` | - | dead data: all rows `false`, nothing reads it (research D3) |
| `ParentCategoryId` | - | unchanged, not in the UI |

`category_translations` (specs/026) - unchanged; the page writes and removes the `en` row through the existing endpoints.

## Audit

`CategoryUpdated` (category Catalog): before `{ Name, Description }`, after the same.
