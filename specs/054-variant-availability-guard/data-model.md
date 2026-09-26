# Data Model: Variant availability guard

> Written on 2026-09-27, after the feature merged (#137), from the code at that merge, the pull request and
> docs/features/catalog.md.

**No table, column, index or migration changed.** The feature changes the condition of one guarded `UPDATE`.

## `product_variants` (Catalog, `ecommerce_catalog_db`)

The columns the guard reads and writes, from `20260922041512_AddProductVariants` (specs/020):

| Column | Type | Meaning |
| :--- | :--- | :--- |
| `Id` | `uuid`, PK | The variant; the first variant of a product reuses the product's id |
| `Availability` | `boolean`, not null, default `false` | Inventory's latest word, as recorded |
| `AvailabilityObservedAt` | `timestamp with time zone`, null | When Inventory observed it - the guard's clock |
| `UpdatedAt` | `timestamp with time zone` | Set on every recorded announcement |

## The guarded write (`ProductRepository.TryRecordVariantAvailabilityAsync`)

Before:

```sql
UPDATE product_variants SET "Availability" = @isAvailable, "AvailabilityObservedAt" = @observedAt, "UpdatedAt" = now
WHERE "Id" = @variantId
  AND ("AvailabilityObservedAt" IS NULL OR "AvailabilityObservedAt" < @observedAt)
  AND ("Availability" <> @isAvailable OR "AvailabilityObservedAt" IS NULL)      -- removed
```

After: the same without the last line. (Written by EF Core's `ExecuteUpdateAsync`; the SQL above is its shape,
not a copy of the generated text.)

When the update changes a row, `RecomputeProductRollupAsync` sets the product's `Availability` to `BOOL_OR` of
its active variants and `AvailabilityObservedAt` to their `MAX`, in one statement.

## State

| Held | Arriving | Result |
| :--- | :--- | :--- |
| nothing (`ObservedAt` null) | anything | recorded |
| observed at T | observed after T, **any** value | recorded (before this feature: only if the value differed) |
| observed at T | observed at T | nothing (duplicate) |
| observed at T | observed before T | nothing (overtaken) |
