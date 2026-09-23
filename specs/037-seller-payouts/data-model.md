# Data model: What the shop owes each seller

All additive; one migration `AddSellerPayouts`.

## orders

| Column | Type | Notes |
| :-- | :-- | :-- |
| `CommissionRate` | `numeric(5,4) NULL` | The marketplace rate at checkout. Null before this feature. |

## order_shipments

| Column | Type | Notes |
| :-- | :-- | :-- |
| `GoodsTotal` | `numeric(18,2) NULL` | Σ unit price × quantity of this part's lines, before tax. |
| `Commission` | `numeric(18,2) NULL` | `round(GoodsTotal × CommissionRate)`; 0 on the shop's own part. |
| `ShippingShare` | `numeric(18,2) NULL` | This part's share of the order's delivery charge. |
| `PayoutId` | `uuid NULL` → `payouts.Id` | Set once, by the payout that covers the part. Indexed. |

The three amounts are all null or all set (CHECK). Null: terms not recorded (research D6).

## payouts (new)

| Column | Type | Notes |
| :-- | :-- | :-- |
| `Id` | `uuid` PK | v7, `ValueGeneratedNever`. |
| `SellerId` | `uuid` | Indexed. |
| `Currency` | `varchar(3)` | |
| `Amount` | `numeric(18,2)` | Σ (GoodsTotal − Commission + ShippingShare) over the parts it claimed. |
| `PartCount` | `int` | How many parts it covers. |
| `RecordedBy` | `uuid` | The administrator, from the token. |
| `CreatedAt` | `timestamptz` | |

## States of a seller's part, for money

`not recorded` (no GoodsTotal) · `on the way` (paid order, not Shipped) · `due` (Shipped, no PayoutId)
· `paid out` (PayoutId set). Only paid orders (`Sales.Statuses`) are in any of the last three.
