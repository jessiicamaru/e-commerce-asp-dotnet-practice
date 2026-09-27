# Data Model: A seller cancels the part of an order they cannot fulfil

**Feature**: [spec.md](spec.md) | **Date**: 2026-09-27

Every change is expand only: nullable columns and a widened filter on a unique index.

## Order: `order_shipments` (migration `AddPartCancellation`)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `CancelledAt` | `timestamptz` null | **New.** When the part was cancelled. Null means not cancelled. |
| `CancelReason` | `varchar(500)` null | **New.** What the buyer reads. |
| `CancelledBy` | `varchar(16)` null | **New.** `Seller` or `Staff`. |
| `CancelRefund` | `decimal(18,2)` null | **New.** What the part refunded (research D2). Null for the last part, which refunded through the whole order. |

## Payment: `refunds` (migration `AddPartRefunds`)

| Column / index | Notes |
| :-- | :-- |
| `PartId` `uuid` null | **New.** The cancelled part (an `order_shipments.Id`). |
| `IX_refunds_PartId` | **New.** Unique where `"PartId" IS NOT NULL`: one refund per part. |
| `IX_refunds_OrderId` | Filter widened from `"ReturnId" IS NULL` to `"ReturnId" IS NULL AND "PartId" IS NULL`: one refund of the whole per order. |

## The cancel (Order), one transaction

```text
lock orders row FOR UPDATE
order must be Paid/Preparing (else 409 / 404)
UPDATE order_shipments SET CancelledAt, CancelReason, CancelledBy, CancelRefund
 WHERE OrderId = @o AND SellerId IS NOT DISTINCT FROM @s
   AND CancelledAt IS NULL AND Status IN ('Pending','Preparing')          -- 0 rows: shipped (409) or already (200)
remaining = parts of @o with CancelledAt IS NULL
remaining = 0  -> whole-order cancel (specs/039): Status Cancelled, CancelledBy, OrderCancelledEvent, all vouchers back
remaining > 0  -> OrderPartCancelledEvent, that seller's vouchers back, summary recomputed without cancelled parts
audit, notice, one save, commit
```

## Readers changed

| Reader | Change |
| :-- | :-- |
| Order summary (moves) | Computed over parts with `CancelledAt IS NULL`. |
| Move guard | `CancelledAt IS NULL` added: a cancelled part cannot be prepared or shipped. |
| Whole-order cancel | The "shipped" and "being prepared" checks ignore cancelled parts. |
| `PayoutRepository.Earning` | `CancelledAt IS NULL`: never on the way, due or paid. |
| `OrderInsights` | Revenue less `CancelRefund`; top products and seller lines leave out cancelled parts' lines. |

## Inventory

No schema change. Reservations of the part's variants end `Released` with reason "Returned: part cancelled".

## State transitions

```text
Part:  Pending ──cancel──▶ (Pending, CancelledAt set)
       Preparing ─cancel─▶ (Preparing, CancelledAt set)
       Shipped   ─cancel─▶ 409
Order: Paid/Preparing ──last part cancelled──▶ Cancelled
```
