# Data Model: Categories in a tree - departments and their categories

No migration. The column, its foreign key and its index already exist:

| Table | Column | Meaning |
| :-- | :-- | :-- |
| `categories` | `ParentCategoryId uuid NULL` → `categories.Id`, `ON DELETE RESTRICT`, indexed | null: a department (top level); otherwise the department it sits under |

Rules (checked in Application, `CategoryTree`):

```text
parent is null                              -> a department
parent exists, != self, parent.Parent null  -> a subcategory
self has subcategories                      -> must stay a department (no third level)
department with subcategories               -> cannot be deleted (409)
```

Transitions: `POST /api/categories` (created with or without a parent), `PUT /api/categories/{id}/parent` (moved:
department ↔ subcategory, or between departments). Existing rows all have `NULL` and stay departments.
