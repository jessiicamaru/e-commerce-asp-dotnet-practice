# Data Model: Vouchers (part 2 - the screens)

> Written on 2026-09-27, after the feature merged (#154), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Feature**: [spec.md](spec.md)

**No table, column, index or migration changed**: client only. The tables are specs/069's
([data-model](../069-vouchers/data-model.md)). The client added types mirroring the server's records.

## `client/src/services/order/types.ts`

| Type | Added |
| :-- | :-- |
| `OrderLine` | `discount?: number` - what vouchers took off the line, frozen at purchase |
| `AppliedVoucher` | `{ code, name, isShop, sellerName, benefit, amount }` |
| `Quote`, `Order` | `vouchers?: AppliedVoucher[] \| null` - absent on orders from before vouchers |
| `CheckoutChoice` | `voucherCodes?: string[]` - a request, never a discount |

## `client/src/services/voucher/types.ts` (new)

```ts
export const BENEFITS = ['Percent', 'FixedAmount', 'FreeShipping'] as const
export interface VoucherAmount    { currency: string; fixedValue: number | null; maxDiscount: number | null; minSubtotal: number | null }
export interface VoucherCondition { type: 'NewCustomer' | 'FirstOrderInShop' | 'MinQuantity' | string; value: number | null }
export interface VoucherTarget    { type: 'Product' | 'Variant' | string; id: string }
export interface VoucherSummary   { id; code; name; isPlatform; benefit; percent; status; startsAt; endsAt;
                                    totalLimit; usedCount; perCustomerLimit; amounts; conditions; targets; createdAt }
export interface VoucherPage      { items: VoucherSummary[]; page; pageSize; totalCount }
export interface NewVoucher       { code; name; benefit; percent; startsAt; endsAt; totalLimit; perCustomerLimit;
                                    amounts; conditions; targets }
```

`NewVoucher` has no owner field: whose it is comes from the token.

## Query keys

`queryKeys.myVouchers(page)` = `['vouchers', 'mine', page]`; creating or disabling invalidates `['vouchers']`.
