# Data model: Shoppers see the vouchers they could use

## Order

| Table | Change |
| :-- | :-- |
| `vouchers` | `IsPublic boolean NOT NULL DEFAULT false` - migration `AddVoucherVisibility`. Expand only: an older image neither reads nor writes it, and its inserts get `false` (private). |

An index is not added: the public read filters a handful of rows per shop by `SellerId` (already indexed with the code)
and `Status`, and the whole table is small; the plan would not use one.

Written by: create (`isPublic`), the specs/113 edit (`isPublic`). Read by: `GET /api/vouchers/public`, the owner's
list (`VoucherSummary.IsPublic`).

No change to states.
