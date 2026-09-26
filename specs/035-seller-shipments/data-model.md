# Data model: Each seller ships their own part

> Completed on 2026-09-27, after the feature merged (#79), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. The three sections that were here are kept; types are now
> as the migration writes them, and the state transitions, the lock order and the old-image behaviour are
> added.

## `order_shipments` (new, Order database)

| Column | Type | Null | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | uuid | no | |
| `OrderId` | uuid | no | FK → orders, cascade |
| `SellerId` | uuid | yes | Whose part; null = the shop's own goods |
| `Status` | varchar(16) | no | `Pending` / `Preparing` / `Shipped` |
| `TrackingReference` | varchar(100) | yes | Set when shipped |
| `UpdatedAt` | timestamptz | no | |

Unique `IX_order_shipments_OrderId_SellerId` **NULLS NOT DISTINCT**. Index on `SellerId`.

As the migration writes them: `uuid`, `uuid`, `uuid NULL`, `character varying(16)`,
`character varying(100) NULL`, `timestamp with time zone`; primary key `PK_order_shipments`; foreign key
`FK_order_shipments_orders_OrderId` with `ON DELETE CASCADE`; the unique index carries the Npgsql
annotation `NullsDistinct = false`; `IX_order_shipments_SellerId` is plain. `Status` is stored as text
through `HasConversion<string>()`; `Id` is `ValueGeneratedNever()` because the application mints it and a
part staged through `Order.Shipments` must be inserted, not taken for an existing row.

## Migration `AddOrderShipments`

`20260923093713_AddOrderShipments`.

1. Create the table and indexes.
2. Backfill: one part per `(OrderId, SellerId)` found in `order_items`, status from the order -
   `Preparing` → `Preparing`, `Shipped` → `Shipped` (with the order's tracking reference), anything
   else → `Pending`.

Additive: an older image ignores the table. `orders.Status` keeps its values.

The backfill SQL, as it ran (a copy of the rule `OrderRepository.EnsureShipmentsAsync` runs per order,
kept separate on purpose - a migration must not change when the repository does):

```sql
INSERT INTO order_shipments ("Id", "OrderId", "SellerId", "Status", "TrackingReference", "UpdatedAt")
SELECT gen_random_uuid(), o."Id", i."SellerId",
       CASE o."Status" WHEN 'Preparing' THEN 'Preparing' WHEN 'Shipped' THEN 'Shipped' ELSE 'Pending' END,
       CASE WHEN o."Status" = 'Shipped' THEN o."TrackingReference" END,
       o."UpdatedAt"
  FROM orders o
  JOIN (SELECT DISTINCT "OrderId", "SellerId" FROM order_items) i ON i."OrderId" = o."Id"
ON CONFLICT ("OrderId", "SellerId") DO NOTHING;
```

Every order gets parts, including failed and still-settling ones (they start `Pending` and can never
move, because a move first checks the order is paid). The pull request recorded **0** orders left
without parts on the local stack. `Down` drops the table.

## `orders` - unchanged columns, new meaning

`Status` and `TrackingReference` become a **summary** of the parts, written by the same transaction
that moves a part (research D4).

| Parts | `orders.Status` | `orders.TrackingReference` |
| :-- | :-- | :-- |
| none started | unchanged (`Paid`, or `Completed` for an old row) | unchanged |
| some started, not all shipped | `Preparing` | the part's if exactly one part, else null |
| all shipped | `Shipped` | the part's if exactly one part, else null |

No value was added to `OrderStatus`.

## Checkout

`SubmitOrderCommandHandler` stages one `OrderShipment` per distinct `SellerId` on the order's lines (null
for the shop's) in the same save as the order, `Status = Pending`.

## State transitions of a part

```text
Pending ──prepare──▶ Preparing ──ship(tracking)──▶ Shipped
```

| From | Step | Guard (single `UPDATE`) | Result when zero rows |
| :-- | :-- | :-- | :-- |
| `Pending` | prepare | `WHERE OrderId = @o AND SellerId IS NOT DISTINCT FROM @s AND Status = 'Pending'` | already `Preparing` → no-op 200; otherwise 409 |
| `Preparing` | ship | same, `Status = 'Preparing'`, sets `TrackingReference` | already `Shipped` with the same reference → no-op 200; a different reference or any other state → 409 |

A part reads as `Paid` while `Pending` (`OrderMapping.Describe`), so no client needs a new word.

## One move, in order (`OrderRepository.TryMoveShipmentAsync`)

Inside `CreateExecutionStrategy().ExecuteAsync` and one transaction:

1. `SELECT 1 FROM orders WHERE "Id" = @o FOR UPDATE` - serialises every move on this order.
2. Read the order's status; not found → "no such part"; not `Paid`/`Completed`/`Preparing`/`Shipped` →
   "order not paid".
3. `EnsureShipmentsAsync` - the backfill rule for this one order, `ON CONFLICT DO NOTHING`.
4. The guarded `UPDATE` of the part.
5. Zero rows: commit (keeping any parts step 3 created) and report where the part stands.
6. Otherwise read every part, write the summary into `orders` with `ExecuteUpdateAsync`, commit.

## What an older image sees

It ignores `order_shipments`, reads `orders.Status` in values it knows, and writes new orders without
parts. The next move on such an order creates its parts in the order's state (step 3), so an order the
older image shipped becomes a shipped part, not one waiting to be shipped again. The existing
`FulfilmentTests` seed orders with no parts, so their passing unchanged exercises this path.
