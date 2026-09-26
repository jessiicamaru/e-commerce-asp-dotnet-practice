# Phase 1 Data Model: A Storefront That Looks Like a Shop

> Written on 2026-09-27, after the feature merged (#62), from the code at that merge, the pull request and docs/architecture/storefront.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed.** #62 contains no migration. The redesign is
presentation only, and category deletion removes rows through keys that already existed.

---

## `categories` in `ecommerce_catalog_db` - rows removed, shape unchanged

`DELETE /api/categories/{id}` removes one `categories` row, and only when
`SELECT count(*) FROM products WHERE "CategoryId" = @id` is zero.

| Relationship | Rule at this merge | Consequence |
| :--- | :--- | :--- |
| `products."CategoryId"` → `categories."Id"` | `ON DELETE RESTRICT` (`ProductConfiguration`) | A category with products cannot be deleted even if the count check were skipped or raced; the check turns the constraint error into a 409 with the count |

The count and the delete are two statements with no lock between them. A product filed under the
category in between is caught by the `RESTRICT` key, surfacing as a database error rather than the
worded 409; that window is not tested.

Category translations did not exist yet (specs/026 added `category_translations`, cascading from the
category).

---

## Client state

No client-side persistence was added. The search term lives in the address (`?q=`), exactly where the
catalogue page already read it; the placeholder tint is computed from the product id on each render and
stored nowhere.
