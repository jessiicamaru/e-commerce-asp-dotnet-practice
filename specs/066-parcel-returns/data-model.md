# Data Model: Returning a delivered parcel

> Written on 2026-09-27, after the feature merged (#149), from the code at that merge, the pull request and
> docs/features/returns.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

Three migrations, one per service. Each only adds, except Payment's, which re-creates one index as partial.

---

## Order - `parcel_returns` (migration `20260925084738_AddParcelReturns`)

One row per returned parcel. Entity `ParcelReturn`, configuration `ParcelReturnConfiguration`.

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | uuid | PK, `ValueGeneratedNever` | `Guid.CreateVersion7()` at request |
| `OrderId` | uuid | not null | The order |
| `ShipmentId` | uuid | not null, **unique**, FK → `order_shipments.Id` `ON DELETE CASCADE` | The parcel. Unique: a parcel is returned once or not at all |
| `CustomerId` | uuid | not null | The buyer, from their token |
| `SellerId` | uuid | null, indexed | Whose parcel; null for the shop's own |
| `Status` | varchar(16) | not null | `ReturnStatus` as text |
| `Reason` | varchar(1000) | not null | Why the buyer wants to return it |
| `DecisionReason` | varchar(500) | null | Why it was refused or rejected |
| `TrackingReference` | varchar(100) | null | How the buyer sent it back |
| `RequestedAt` | timestamptz | not null | |
| `DecidedAt` | timestamptz | null | Last accept/refuse/reject; the buyer's window after a decision counts from here |
| `SentBackAt` | timestamptz | null | |
| `ReceivedAt` | timestamptz | null | |
| `RefundAmount` | numeric(18,2) | null | Goods plus tax, written by "received" |
| `UpdatedAt` | timestamptz | not null | Orders the staff queue (oldest first) |

**Indexes**: `IX_parcel_returns_ShipmentId` (unique - the target of `ON CONFLICT ("ShipmentId") DO NOTHING`),
`IX_parcel_returns_Status_RequestedAt`, `IX_parcel_returns_SellerId`.

**Navigation**: `OrderShipment.Return` (one-to-one), which the payout queries use as `s.Return`.

The status is text, like every status in Order, so a value added later never stops an earlier image reading the row.

### State transitions

```text
Requested ─accept→ Accepted ─buyer sends (tracking)→ SentBack ─received→ Received → refund + restock
    └─refuse→ Refused ─buyer escalates→ Escalated ─admin→ Accepted | Rejected (final)
```

| Transition | Who | Guard (in the `UPDATE ... WHERE`) | Writes |
| :-- | :-- | :-- | :-- |
| insert `Requested` | buyer | delivered, `DeliveredAt > now - window`, not cancelled (checked before); `ON CONFLICT DO NOTHING` | `Reason`, `RequestedAt` |
| `Requested` → `Accepted` / `Refused` | parcel's seller; Admin for the shop's parcel | `Status = 'Requested'` | `DecidedAt`, `DecisionReason` |
| `Refused` → `Escalated` | buyer | `Status = 'Refused' AND DecidedAt > now - window` | `UpdatedAt` |
| `Escalated` → `Accepted` / `Rejected` | Admin | `Status = 'Escalated'` | `DecidedAt`, `DecisionReason` |
| `Accepted` → `SentBack` | buyer | `Status = 'Accepted' AND DecidedAt > now - window` | `TrackingReference`, `SentBackAt` |
| `SentBack` → `Received` | parcel's seller; Admin for the shop's parcel | `Status = 'SentBack'` | `ReceivedAt`, `RefundAmount`; stages `ParcelReturnedEvent` |

`Rejected` and `Received` are final. An `Accepted` or `Refused` return the buyer does nothing about closes with its
window: no row changes, but it stops holding the money (below).

Each transition is one `ExecuteUpdateAsync` inside a transaction opened in `CreateExecutionStrategy().ExecuteAsync`;
when it affects one row, the `stage` callback (audit entry, notice, event) is saved in the same transaction. When it
affects none, the handler answers 409.

---

## Order - what "due" means now (no schema change)

`PayoutRepository` gained `Money(sellerId)`, a projection of each earning part to `Owed`, `PaidOut` and `Due`:

- `Due` = not paid out, `Shipped`, `DeliveredAt <= now - window`, and no open return;
- open = `Requested`, `Escalated`, `SentBack`, or `Accepted`/`Refused` with `DecidedAt > now - window`;
- `Earning()` now excludes a part whose return is `Received` - it is no money at all.

The payout claim's CTE says the same in SQL with `AND s."DeliveredAt" <= @cutoff AND NOT EXISTS (SELECT 1 FROM
parcel_returns r WHERE r."ShipmentId" = s."Id" AND (r."Status" = ANY(@holding) OR (r."Status" = ANY(@deciding) AND
r."DecidedAt" > @cutoff)))`, where `holding` also contains `Received`.

Configuration: `Returns:WindowDays` = 7 in `Ecommerce.Order.WebApi/appsettings.json` (`ReturnOptions`, default 7).

---

## Payment - `refunds.ReturnId` (migration `20260925085607_AddReturnRefunds`)

| Change | Detail |
| :-- | :-- |
| `ReturnId` | uuid, **nullable**, new |
| `IX_refunds_OrderId` | dropped and re-created **unique with filter `"ReturnId" IS NULL`** - one whole-order (cancellation) refund per order |
| `IX_refunds_ReturnId` | new, **unique with filter `"ReturnId" IS NOT NULL`** - one refund per return |

**Old images**: an earlier Payment image inserts whole-order refunds with `ReturnId` null, which the partial index
still keeps unique per order, so a rollback keeps working. The drop-and-recreate of the index is what the
`schema-compatibility` job may comment on; it narrows nothing an old image writes.

---

## Inventory - `returned_parcels` (migration `20260925090112_AddReturnedParcels`)

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `ReturnId` | uuid | PK | The return whose units were put back - the claim |
| `OrderId` | uuid | not null, indexed | For an operator |
| `RestockedAt` | timestamptz | not null | |

Claimed with `INSERT ... ON CONFLICT DO NOTHING` inside the transaction that locks the stock rows (`FOR UPDATE`,
ascending id, like every stock path) and adds `QuantityOnHand`. A redelivery finds the claim and moves nothing.

---

## What did not change

- `order_shipments` and `orders` gained no column and no status value: a returned parcel is still `Shipped` with
  `DeliveredAt` set, and the return is its own row. An earlier Order image reads every row it read before.
- `stock_reservations` is untouched: a returned parcel belongs to an order confirmed long ago, so its units are put
  back from what the event names, not from reservations.
- `payments` is untouched.
