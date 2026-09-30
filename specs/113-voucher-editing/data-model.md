# Data model: A voucher's terms can be corrected

No new table or column, and no migration.

| Table | Columns written by an edit |
| :-- | :-- |
| `vouchers` | `Name`, `EndsAt`, `TotalLimit`, `PerCustomerLimit`, `UpdatedAt` - one guarded `UPDATE` (research D2) |
| `voucher_amounts` | `MinSubtotal` of each currency named, for rows that exist |
| `voucher_conditions` | `Value` of the `MinQuantity` row, if the voucher has one |

Never written by an edit: `Code`, `SellerId`, `Benefit`, `Percent`, `StartsAt`, `Status`, `UsedCount`, `CreatedBy`;
`voucher_amounts.FixedValue` / `MaxDiscount`; `voucher_targets`; `voucher_redemptions`; `voucher_customer_uses`.

## States

Unchanged: `Active` → `Disabled`. Only `Active` is edited.
