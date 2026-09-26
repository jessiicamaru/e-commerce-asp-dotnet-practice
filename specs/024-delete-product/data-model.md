# Phase 1 Data Model: Delete a Product

> Written on 2026-09-27, after the feature merged (#61), from the code at that merge, the pull request and docs/features/catalog.md.

**Feature**: [spec.md](./spec.md) | **Decisions**: [research.md](./research.md)

**No table, column, index or migration was added or changed.** The feature deletes rows through the
foreign keys that already existed; #61 contains no migration in either service. Because nothing in the
schema changed, an earlier Catalog or Inventory image runs against it unchanged.

---

## What a product deletion removes in `ecommerce_catalog_db`

| Table | How it goes | Foreign key it goes by |
| :--- | :--- | :--- |
| `products` | `Remove(product)` | - |
| `product_variants` | `RemoveRange(product.Variants)`, **explicitly** | `ProductId` → `products`, `ON DELETE RESTRICT` (specs/020) |
| `variant_options` | cascade from the variant | `VariantId` → `product_variants`, `CASCADE` |
| `variant_option_translations` | cascade from the option | → `variant_options`, `CASCADE` |
| `variant_prices` | cascade from the variant | `VariantId` → `product_variants`, `CASCADE` |
| `product_translations` | cascade from the product | `ProductId` → `products`, `CASCADE` |
| `OutboxMessage` | one row **added**: the `ProductDeletedEvent` | - |

All of it is one `SaveChangesAsync`, so the deletion and the outbox row commit together or not at all.
The variants are removed explicitly because the `RESTRICT` key would refuse the product otherwise - that
key exists so a product cannot take its variants with it by accident, since orders refer to variants
([research.md D2](./research.md)).

The unique index on `products.Sku` is what `A_deleted_products_sku_can_be_used_again` leans on: had
anything been left behind, creating a product with the same SKU would be refused.

What is **not** removed: the product's image file on the `catalog_images` volume (closed by specs/029),
and the `categories` row the product was filed under.

---

## What the announcement removes in `ecommerce_inventory_db`

```sql
DELETE FROM stock_items WHERE "ProductId" = ANY(@variantIds);   -- ExecuteDeleteAsync, one statement
```

`stock_items."ProductId"` holds a **variant** id since specs/020 (the column was deliberately not
renamed), so the event's `VariantIds` are exactly the keys to match. The statement returns the number of
rows deleted; zero is a normal answer.

**`stock_reservations` is not touched.** A comment in `StockRepository.ForgetAsync` says "Reservations
cascade from the stock row", but at this merge `stock_reservations` has **no foreign key** to
`stock_items` - it carries its own `ProductId` and is joined by value (see
`StockReservationConfiguration` and the model snapshot). Reservations for a deleted variant therefore
stay, in whatever state they were in. The comment is wrong about the schema; the consequence is small,
because a variant nobody can order gains no new reservation, and the expiry sweeper
(`ExpireStockCommandHandler`) already skips the stock adjustment when it finds no stock row, so an old
`Held` reservation is still marked `Expired`. How the release and confirm paths treat a reservation
whose stock row is gone was not examined for this record.

---

## State

A product has no deletion state: it exists, or it does not. Deletion is not a transition on
`IsActive` - an inactive product still answers `GET /api/products/{id}` with `isActive: false`, and a
deleted one answers 404.
