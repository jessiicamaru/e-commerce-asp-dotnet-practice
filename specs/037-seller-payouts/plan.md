# Implementation Plan: What the shop owes each seller

**Branch**: `037-seller-payouts` | **Spec**: [spec.md](spec.md) | [research](research.md) |
[data model](data-model.md) | [contracts](contracts/api.md) | [quickstart](quickstart.md)

## Technical Context

- **Order only.** Configuration `Marketplace:CommissionRate` (0.10), read by `ConfiguredCommissionRate`
  (`ICommissionRate`) and checked at startup like `Tax`.
- **Checkout**: `Earnings.SplitDelivery` and `Earnings.ForPart` (pure) fill each part's `GoodsTotal`,
  `Commission`, `ShippingShare`; the order records `CommissionRate`. `CheckoutPricing` gains the
  currency's decimals on its result so the split rounds right.
- **Reads**: sale summary and detail gain earnings; `GetMyBalanceQuery`, `GetMyPayoutsQuery`,
  `GetPayoutsDueQuery`.
- **Write**: `RecordPayoutCommand` → `IPayoutRepository.TryRecordAsync` (research D5): one SQL
  statement, so there is no transaction for the caller to open.
- **Client**: seller console gains "Thanh toán" (`/shop/payouts`): balance per currency and the payout
  history; sale detail shows goods, commission, delivery share and payout.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I - Service autonomy | Order's own tables only; no new call, no new event. |
| II - Clean Architecture | Split and earnings are pure Application code; SQL in Infrastructure. |
| III - Atomic writes | Earnings written in the order's own save. A payout's claim, sum and row are one transaction; the claim is a guarded UPDATE. |
| IV - Identity from the token | Seller from `ICurrentUser`; `RecordedBy` from the token, never the body. |
| V - Evidence | Tests: split sums exactly; rate change leaves sales unchanged; failed orders never owed; unshipped not due; concurrent payouts claim each part once; nothing due is 409 with no row; a seller cannot see another's. |
| Schema compatibility | Additive nullable columns and a new table. An older image ignores them; parts it creates on demand record no terms (research D6). |

No Complexity Tracking entries.

## Verification

Order tests; client tests; Bruno; `verify-saga.sh`; end to end on the demo data - a fresh two-seller
order, both shipped, the balance, a payout, a second payout refused.
