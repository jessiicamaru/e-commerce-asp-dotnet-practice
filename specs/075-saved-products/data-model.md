# Data Model: A shopper saves a product for later

> Written on 2026-09-27, after the feature merged (#159), from the code at that merge, the pull request and docs/features/saved-products.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

One new table in `ecommerce_catalog_db`, added by one migration. No existing column changed. One repository method
changed its return type, and one notification kind was declared.

---

## `saved_products`

One row per shopper per product. Entity `SavedProduct`
([SavedProduct.cs](../../server/src/Services/Catalog/Ecommerce.Catalog.Domain/Entities/SavedProduct.cs)), mapped by
[SavedProductConfiguration.cs](../../server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Configurations/SavedProductConfiguration.cs).

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `CustomerId` | `uuid` | Not null, part of the primary key | The shopper, from the token's subject (`ICurrentUser.Id`), never from the request |
| `ProductId` | `uuid` | Not null, part of the primary key, FK to `products."Id"` `ON DELETE CASCADE` | The saved product - the product, not a variant |
| `SavedAt` | `timestamp with time zone` | Not null | When it was **first** saved; a repeat save does not move it (D5) |

**Keys and constraints**:

- `PK_saved_products` on `(CustomerId, ProductId)`. This is what `ON CONFLICT ("CustomerId", "ProductId") DO
  NOTHING` names: saving twice, or twenty times at once, inserts once (FR-001, research D5).
- `FK_saved_products_products_ProductId` to `products ("Id")`, `ON DELETE CASCADE`: a product deleted outright
  (`DELETE /api/products/{id}`, specs/024) takes its saved rows with it (research D2). A product withdrawn,
  rejected or taken down is not deleted, so its rows stay.

**Indexes**:

| Name | Columns | Serves |
| :-- | :-- | :-- |
| `IX_saved_products_CustomerId_SavedAt` | `(CustomerId, SavedAt)` | A shopper's page, newest first (`ORDER BY "SavedAt" DESC, "ProductId"`), and the ids |
| `IX_saved_products_ProductId` | `(ProductId)` | "Who saved this?" - the savers of a product that came back in stock, and the cascade |

The key's leading column also serves the per-shopper count and the delete.

**No surrogate id, no `ValueGeneratedNever()` needed.** The key is composite and application-supplied, and rows
are inserted by raw SQL (`ExecuteSqlInterpolatedAsync`), not through the change tracker.

**No state.** A row exists or it does not; there are no transitions. Whether the product can be bought is not
stored here - it is read from `products` on every request (research D6).

---

## Migration `20260926095302_AddSavedProducts`

[20260926095302_AddSavedProducts.cs](../../server/src/Services/Catalog/Ecommerce.Catalog.Infrastructure/Migrations/20260926095302_AddSavedProducts.cs),
in `Ecommerce.Catalog.Infrastructure`.

- `Up`: `CreateTable("saved_products")` with the three columns, the primary key and the cascading foreign key;
  `CreateIndex` for the two indexes above.
- `Down`: `DropTable("saved_products")`.

**Compatibility with an earlier image**: the migration only adds a table. A Catalog image from before it never
reads or writes `saved_products` and runs against the migrated database unchanged (PR #159, "Migration"). The
`schema-compatibility` job has nothing to flag: no drop, rename or narrowing.

`CatalogDbContext` gains `DbSet<SavedProduct> SavedProducts`; `CatalogDbContextModelSnapshot` records the entity.

---

## What changed without a schema change

### `IProductRepository.RecomputeProductRollupAsync` now returns the flip

`Task RecomputeProductRollupAsync(Guid productId, ...)` became `Task<bool>`. The statement is still the one
`UPDATE products` that derives `Price`, `Availability` and `AvailabilityObservedAt` from the product's active
variants (specs/020), with two additions (research D4):

```sql
WITH before AS (SELECT "Availability" AS was FROM products WHERE "Id" = @productId)
UPDATE products p SET ... WHERE p."Id" = @productId
RETURNING (SELECT was FROM before) AS "Was", p."Availability" AS "Now"
```

It returns true only when exactly one row came back with `Was = false` and `Now = true`. The columns it writes
are unchanged, so an earlier image that reads `products.Availability` reads the same values.

### A notification kind

`SavedBackInStock` is added to `NotificationKind` in
[Notifier.cs](../../server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs) and declared in
[notification-kinds.json](../../server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json)
as `"SavedBackInStock": { "required": ["product"] }`. The notice itself is stored by Activity in its existing
notifications table (specs/042); Activity's schema did not change. Since specs/078 the same file declares
placeholders, and `{{product}}` is made from the `product` key this kind carries, so the kind's wording can use it.

---

## What did NOT change

- **`products`** - no new column. Availability stays the rollup Inventory's announcements feed (specs/004,
  specs/020); nothing about a product records who saved it.
- **Inventory, Cart, Order** - untouched. A saved product reserves nothing and is not in the cart.
- **No new message contract.** The notice travels as the existing `UserNotificationRequested`
  ([contracts/messages.md](./contracts/messages.md)).
