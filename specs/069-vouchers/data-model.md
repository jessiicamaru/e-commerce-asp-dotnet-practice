# Data Model: Vouchers (part 1 - the server)

> Written on 2026-09-27, after the feature merged (#153), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature**: [spec.md](spec.md) | **Decisions**: [research.md](research.md)

All in Order's database (`ecommerce_order_db`), one migration: `20260926073015_AddVouchers`. Entities in
`Ecommerce.Order.Domain/Entities/Voucher.cs`, configuration in `VoucherConfiguration.cs`. Enums are stored as text.

---

## `vouchers`

| Column | Type | Constraints | Meaning |
| :-- | :-- | :-- | :-- |
| `Id` | uuid | PK | `Guid.CreateVersion7()` |
| `Code` | varchar(32) | not null, **unique** (`IX_vouchers_Code`) | Stored upper-case; shape `^[A-Z0-9][A-Z0-9-]{2,31}$` |
| `SellerId` | uuid | null | Null = the platform's; else the seller's shop voucher |
| `Name` | varchar(100) | not null | |
| `Benefit` | varchar(16) | not null | `Percent`, `FixedAmount`, `FreeShipping` |
| `Percent` | numeric(5,2) | null | 1-100, only for `Percent` |
| `StartsAt` | timestamptz | not null | Defaults to creation time |
| `EndsAt` | timestamptz | null | After `StartsAt` |
| `Status` | varchar(16) | not null | `Active`, `Disabled` |
| `TotalLimit` | int | null | ≥ 1 when set |
| `UsedCount` | int | not null | The guarded counter |
| `PerCustomerLimit` | int | null | ≥ 1, ≤ `TotalLimit` |
| `CreatedBy` | uuid | not null | The token's subject |
| `CreatedAt`, `UpdatedAt` | timestamptz | not null | |

**CHECK** `CK_vouchers_used_count`: `"UsedCount" >= 0 AND ("TotalLimit" IS NULL OR "UsedCount" <= "TotalLimit")`.
**Index** `IX_vouchers_SellerId_CreatedAt` for the owner's list.

## `voucher_conditions`

| Column | Type | Constraints |
| :-- | :-- | :-- |
| `VoucherId` | uuid | PK part, FK → `vouchers` cascade |
| `Type` | varchar(32) | PK part: `NewCustomer`, `FirstOrderInShop`, `MinQuantity` |
| `Value` | int | null; 1-1000 for `MinQuantity` only |

`FirstOrderInShop` only on a shop voucher. PK `(VoucherId, Type)`: each condition once.

## `voucher_targets`

| Column | Type | Constraints |
| :-- | :-- | :-- |
| `VoucherId` | uuid | PK part, FK → `vouchers` cascade |
| `Type` | varchar(16) | PK part: `Product`, `Variant` |
| `TargetId` | uuid | PK part |

None means everything the voucher may touch. At most 100. A free-delivery voucher has none.

## `voucher_amounts`

| Column | Type | Constraints |
| :-- | :-- | :-- |
| `VoucherId` | uuid | PK part, FK → `vouchers` cascade |
| `Currency` | varchar(3) | PK part; a supported currency |
| `FixedValue` | numeric(18,2) | null; > 0 and only for `FixedAmount` |
| `MaxDiscount` | numeric(18,2) | null; > 0 - the cap |
| `MinSubtotal` | numeric(18,2) | null; ≥ 0 - the minimum spend on what it applies to |

At least one row. No row for a currency: not usable in it (research D3). Each amount must fit the currency's minor
unit (dong has none).

## `voucher_customer_uses`

| Column | Type | Constraints |
| :-- | :-- | :-- |
| `VoucherId` | uuid | PK part |
| `CustomerId` | uuid | PK part |
| `Uses` | int | not null, CHECK `CK_voucher_customer_uses_uses` `"Uses" >= 0` |

Upserted by the claim with `ON CONFLICT ("VoucherId", "CustomerId") DO UPDATE SET "Uses" = "Uses" + 1 WHERE "Uses" <
@limit RETURNING`; kept for every voucher so a release is symmetrical.

## `voucher_redemptions`

| Column | Type | Constraints |
| :-- | :-- | :-- |
| `Id` | uuid | PK |
| `VoucherId` | uuid | FK → `vouchers` **restrict** |
| `OrderId` | uuid | FK → `orders` cascade, indexed |
| `CustomerId` | uuid | not null |
| `Code` | varchar(32) | frozen |
| `Name` | varchar(100) | frozen |
| `SellerId` | uuid | null - frozen whose |
| `Benefit` | varchar(16) | frozen |
| `Amount` | numeric(18,2) | what it took off, in `Currency` |
| `Currency` | varchar(3) | the order's |
| `CreatedAt` | timestamptz | |
| `ReleasedAt` | timestamptz | null until a failed or cancelled order gives the use back |

**Unique** `IX_voucher_redemptions_VoucherId_OrderId`: one redemption of a voucher per order. Navigation `Order.Vouchers`.

## `order_items` - two columns

| Column | Type | Default |
| :-- | :-- | :-- |
| `ShopDiscount` | numeric(18,2) not null | 0 |
| `PlatformDiscount` | numeric(18,2) not null | 0 |

**CHECK** `CK_order_items_discounts`: `"ShopDiscount" >= 0 AND "PlatformDiscount" >= 0 AND "ShopDiscount" +
"PlatformDiscount" <= "UnitPrice" * "Quantity"`.

## `orders` - one CHECK replaced

- Dropped: `CK_orders_no_discount_yet` (`"DiscountTotal" IS NULL OR "DiscountTotal" = 0`, from specs/012).
- Added: `CK_orders_discount_not_negative` (`"DiscountTotal" IS NULL OR "DiscountTotal" >= 0`).

The existing parts CHECK (`Subtotal + ShippingPrice + TaxTotal - DiscountTotal = TotalAmount`) is unchanged and holds.

---

## State

A voucher is `Active` → `Disabled` (one guarded `UPDATE ... WHERE "Status" = 'Active'`), never back. A redemption is
held → released (`ReleasedAt` set once by the release CTE).

| Transition | Statement | Guard |
| :-- | :-- | :-- |
| claim, total | `UPDATE vouchers SET "UsedCount" = "UsedCount" + 1` | `"Status" = 'Active' AND ("TotalLimit" IS NULL OR "UsedCount" < "TotalLimit")` |
| claim, customer | upsert `voucher_customer_uses` | `"Uses" < limit` |
| release | CTE: `UPDATE voucher_redemptions SET "ReleasedAt"` → `UsedCount - 1` → `Uses - 1` | `"ReleasedAt" IS NULL`; counters `> 0` |
| disable | `UPDATE vouchers SET "Status" = 'Disabled'` | `"Status" = 'Active'` |

## Old images

The new columns default to 0 and the new CHECKs accept what an earlier image writes (no discount); the dropped CHECK
only relaxes a rule. An earlier image does not read the new tables. Expand-only apart from that relaxation.
