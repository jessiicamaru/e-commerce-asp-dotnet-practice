# Data Model: A deleted product's holds are released with its stock

**No table, column, index, constraint or migration changes** (FR-005). The change is one guarded `UPDATE` on an
existing table.

## `stock_reservations` (Inventory, `ecommerce_inventory_db`, 5437) - existing

| Column | Type | Written by this feature |
| :-- | :-- | :-- |
| `Id` | `uuid` | - |
| `OrderId` | `uuid` | - |
| `ProductId` | `uuid` (a variant id since specs/020) | - (the filter) |
| `Quantity` | `integer`, CHECK > 0 | - |
| `Status` | `varchar(16)`, enum as text | `Held` → `Released` |
| `ExpiresAt` | `timestamptz` | - |
| `CreatedAt` | `timestamptz` | - |
| `SettledAt` | `timestamptz`, null | now |
| `SettlementReason` | `varchar(512)`, null | `Product deleted` |

Indexes (unchanged): unique `(OrderId, ProductId)`; `OrderId`; `(Status, ExpiresAt)` for the sweeper. There is **no
foreign key** to `stock_items` (research D2).

## What `ForgetProductCommand` does, in one transaction

```sql
DELETE FROM stock_items WHERE "ProductId" = ANY(@variantIds);
UPDATE stock_reservations
   SET "Status" = 'Released', "SettledAt" = @now, "SettlementReason" = 'Product deleted'
 WHERE "ProductId" = ANY(@variantIds) AND "Status" = 'Held';
```

## Reservation states

```text
            ┌── order completes ───────────▶ Confirmed ──(order cancelled, specs/039)──▶ Released
  Held ─────┼── order fails ───────────────▶ Released
            ├── deadline passes (sweeper) ─▶ Expired
            └── product deleted (#181) ────▶ Released, "Product deleted"        ← new transition

  Confirmed / Released / Expired ──product deleted──▶ unchanged (history)
```
