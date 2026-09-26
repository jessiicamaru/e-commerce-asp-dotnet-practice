# Data Model: Revenue counts on the day an order was paid

> Written on 2026-09-27, after the feature merged (#156), from the code at that merge, the pull request and
> docs/features/admin-insights.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

## `orders` - one column (migration `20260926085159_AddOrderPaidAt`)

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `PaidAt` | timestamp with time zone | **nullable**, no index, no default | When the saga settled the order to `Paid` |

Written only by the settlement's guarded statement:

```sql
UPDATE orders SET "Status" = @settled, …, "UpdatedAt" = @settledAt,
                  "PaidAt" = CASE WHEN @settled = 'Paid' THEN @settledAt ELSE NULL END
 WHERE "Id" = @id AND "Status" = 'Submitted'
```

(in EF: `.SetProperty(x => x.PaidAt, settledStatus == OrderStatus.Paid ? settledAt : null)` in `SettleAsync`).

| State | `PaidAt` |
| :-- | :-- |
| `Submitted` (settling) | null |
| settled `Paid` | the saga's `CompletedAt` |
| settled `Failed` | null |
| any order from before this migration | null |

## Reads that changed

`OrderInsights`: `SoldIn` filters `(PaidAt ?? CreatedAt) >= from AND < to`; the admin's revenue groups by
`(PaidAt ?? CreatedAt).Date`; `SellerLinesIn` sets `Day = (PaidAt ?? CreatedAt).Date`. Top products and top buyers go
through `SoldIn`, so they follow.

## Old images

Expand only. An earlier image never writes `PaidAt` and never reads it; orders it settles during a rollback have
`PaidAt` null and fall back to `CreatedAt`.
