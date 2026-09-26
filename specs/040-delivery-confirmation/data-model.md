# Data Model: Confirming a parcel arrived

> Written on 2026-09-27, after the feature merged (#85), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

One additive migration in `ecommerce_order_db`. **No enum value added**; `orders` untouched.

---

## `order_shipments` — three columns and an index

Migration `20260923183204_AddShipmentDelivery`.

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `ShippedAt` | `timestamp with time zone` | yes | when the part moved to `Shipped`; what the automatic week counts from |
| `DeliveredAt` | `timestamp with time zone` | yes | when it was delivered; null until then |
| `DeliveryConfirmedBy` | `character varying(16)` | yes | `Customer` or `Auto` (`ParcelDelivery.ByCustomer` / `ByAuto`) |

Index `IX_order_shipments_Status_DeliveredAt_ShippedAt` on `(Status, DeliveredAt, ShippedAt)` - the
sweep's question: shipped, not delivered, shipped before the cutoff.

**Backfill**, in the same migration:

```sql
UPDATE order_shipments SET "ShippedAt" = "UpdatedAt" WHERE "Status" = 'Shipped' AND "ShippedAt" IS NULL;
```

True for those rows because nothing moves a part after it ships. Without it, parcels shipped before
this would never be auto-confirmed.

**Schema compatibility**: additive - three nullable columns and an index. An older image ignores them.
A part an older image ships during a rollback gets no `ShippedAt`, and the sweep skips a null
`ShippedAt`, so that parcel waits for its customer's confirmation; nothing in this feature backfills it
later (the migration's backfill ran once).

## Who writes each column

| Writer | Writes |
| :-- | :-- |
| `TryMoveShipmentAsync` (the ship move) | `ShippedAt = at` when moving to `Shipped`, null otherwise |
| `EnsureShipmentsAsync` (parts created on demand) | `ShippedAt = orders.UpdatedAt` when the order is `Shipped` |
| `TryConfirmDeliveryAsync` (the customer) | `DeliveredAt = at`, `DeliveryConfirmedBy = 'Customer'` |
| `AutoConfirmDeliveriesAsync` (the sweep) | `DeliveredAt = at`, `DeliveryConfirmedBy = 'Auto'` |

## State

`Status` stays `Pending` → `Preparing` → `Shipped` (specs/035). Delivered is a fact beside it:

```text
Shipped, DeliveredAt NULL ──customer confirms──────────────────▶ Shipped, DeliveredAt set, by Customer
                          └─sweep, ShippedAt <= now - N days───▶ Shipped, DeliveredAt set, by Auto
```

The two guards, each a single `UPDATE` (EF `ExecuteUpdateAsync`):

```sql
-- the customer
UPDATE order_shipments s SET "DeliveredAt" = @at, "DeliveryConfirmedBy" = 'Customer'
 WHERE s."Id" = @shipment AND s."OrderId" = @order
   AND EXISTS (SELECT 1 FROM orders o WHERE o."Id" = s."OrderId" AND o."UserId" = @owner)
   AND s."Status" = 'Shipped' AND s."DeliveredAt" IS NULL;

-- the sweep
UPDATE order_shipments SET "DeliveredAt" = @at, "DeliveryConfirmedBy" = 'Auto'
 WHERE "Status" = 'Shipped' AND "DeliveredAt" IS NULL
   AND "ShippedAt" IS NOT NULL AND "ShippedAt" <= @cutoff;
```

(Shown as SQL for reading; the owner is expressed through the `Order` navigation.) Whichever runs first
sets the row; the other affects nothing, so the first word stands.

"Delivered" for the whole order is derived by the client when every part has `deliveredAt`; `orders.Status`
stays `Shipped`.

## Money

`PayoutRepository`:

| Figure | Before (specs/037) | Since this feature |
| :-- | :-- | :-- |
| on the way | part not `Shipped` | part `DeliveredAt IS NULL` |
| due | part `Shipped`, not paid out | part `DeliveredAt IS NOT NULL`, not paid out |
| the payout claim | `Status = 'Shipped' AND PayoutId IS NULL` | also `AND "DeliveredAt" IS NOT NULL` |

(specs/066 later required the delivery to be older than the return window as well.)
