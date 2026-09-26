# Phase 1 Data Model: Seed Photographs

> Written on 2026-09-27, after the feature merged (#69), from the code at that merge, the pull request and server/seed/README.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed.** #69 touches no server code. Each upload
goes through Catalog's existing endpoint and changes rows exactly as a person's upload does.

---

## What one upload changes, through Catalog (specs/019, specs/029)

| Where | Change |
| :--- | :--- |
| Image store | A new file keyed `{productId:N}-{ImageUpdatedAt ticks}.{ext}`, written first |
| `products` (`ecommerce_catalog_db`) | `ImageContentType` and `ImageUpdatedAt` switched to the new file by a guarded `UPDATE` |
| Image store | The previous file, if any, deleted after the switch |

That order is why a re-run is safe, and the run confirmed it: 14 uploads over 11 existing images left 14
files on the `catalog_images` volume, not 25.

---

## Files on the developer's machine

| Path | Committed | Holds |
| :--- | :--- | :--- |
| `server/seed/images/<SKU>.<ext>` | **No** - gitignored | The staged photographs |
| `server/seed/IMAGE-CREDITS.md` | Yes | SKU, source file, photographer, licence; the placeholder's status |
