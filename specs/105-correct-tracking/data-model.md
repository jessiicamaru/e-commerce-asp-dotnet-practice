# Data Model: A mistyped tracking reference can be corrected

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27

No new table or column, and no migration. The feature writes an existing column under a new guard.

## The correction (Order), one transaction

```text
lock orders row FOR UPDATE
order must be Paid/Preparing/Shipped, else 404 (seller) / 409 (staff)
UPDATE order_shipments SET "TrackingReference" = @new, "UpdatedAt" = now
 WHERE "OrderId" = @o AND "SellerId" IS NOT DISTINCT FROM @s
   AND "Status" = 'Shipped' AND "DeliveredAt" IS NULL AND "CancelledAt" IS NULL
   AND "TrackingReference" IS DISTINCT FROM @new
0 rows -> read the part: the same reference (200, no-op), delivered / not shipped / cancelled (409), none (404)
order summary rewritten (orders."TrackingReference" is the part's when there is one part)
audit TrackingCorrected {before, after}; notice TrackingCorrected to the buyer
save, commit
```

`ShippedAt` is not in the `SET` (research D3).

## Notices (`notification-kinds.json`)

| Kind | Required | Optional |
| :-- | :-- | :-- |
| `TrackingCorrected` | `orderId`, `tracking` | `shop` |

## State transitions

None. The part stays `Shipped`.
