# Data Model: A banned seller's shop is closed

## Catalog migration `SellerSuspension` (expand only)

| Table | Column | Type | Default | Meaning |
| :-- | :-- | :-- | :-- | :-- |
| `products` | `SellerSuspended` | `boolean`, not null | `false` | the product's seller is banned; off the shelf |
| `sellers` | `Suspended` | `boolean`, not null | `false` | the read model's copy of the suspension |
| `sellers` | `SuspensionChangedAt` | `timestamptz`, null | - | when Identity decided it; the ordering guard |

No row is rewritten by the migration. An earlier image ignores the columns (and would list a suspended seller's products
during a rollback - spec, Edge Cases).

## Identity - unchanged tables

`users.BannedAt` / `BanReason` (specs/043) and the user's roles decide whether to announce.

## Recording a suspension (Catalog, one transaction)

```sql
INSERT INTO sellers ("SellerId", "ShopName", "ObservedAt", "Suspended", "SuspensionChangedAt")
VALUES (@id, '', '-infinity', @suspended, @at)
ON CONFLICT ("SellerId") DO UPDATE SET "Suspended" = @suspended, "SuspensionChangedAt" = @at
 WHERE sellers."SuspensionChangedAt" IS NULL OR sellers."SuspensionChangedAt" < @at;
-- only when a row was written:
UPDATE products SET "SellerSuspended" = @suspended WHERE "SellerId" = @id;
```

## "On the shelf"

`Product.OnShelf = IsListed && IsActive && !SellerSuspended` - and in SQL
`"ReviewStatus" = 'Approved' AND "IsActive" AND NOT "SellerSuspended"`.

## States

```text
  seller open ──ban (Identity: Seller role)──▶ SellerSuspensionChanged(true) ──▶ products off the shelf
  seller suspended ──ban lifted──────────────▶ SellerSuspensionChanged(false) ─▶ products back as they were
                                                                                  (savers of in-stock ones told)
  lock / unlock ──────────────────────────────▶ nothing (Decision)
```
