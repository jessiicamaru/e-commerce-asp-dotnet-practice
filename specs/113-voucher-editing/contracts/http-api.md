# Contract: editing a voucher

## `PUT /api/vouchers/{id}` - Seller, Admin

```json
{
  "name": "Autumn lenses",
  "endsAt": "2026-11-30T17:00:00Z",
  "totalLimit": 200,
  "perCustomerLimit": 1,
  "minSubtotals": [ { "currency": "VND", "minSubtotal": 5000000 } ],
  "minQuantity": 2
}
```

Every field is the new value: `endsAt`, `totalLimit`, `perCustomerLimit` and `minQuantity` may be null (none);
`minSubtotals` lists currencies the voucher already has, each with a minimum or null. A currency left out keeps its
minimum.

| Answer | When |
| :-- | :-- |
| 200 | The voucher (`VoucherSummary`, as in `GET /api/vouchers/mine`). |
| 400 | Name empty or over 100; `endsAt` not after the start and now; a limit under 1; per-customer over total; a currency the voucher does not have, twice, or a minimum below 0 or finer than the currency's minor unit; `minQuantity` outside 1-1000 or given for a voucher with no minimum-quantity condition. |
| 401 / 403 | No token / not Seller or Admin. |
| 404 | No such voucher, or not the caller's (a seller's own; an administrator's: the platform's). |
| 409 | Disabled; or `totalLimit` below the uses already made. |

Audited: `Order` / `VoucherEdited` on the `Voucher`, summary `Voucher {code} edited`, with before and after.
