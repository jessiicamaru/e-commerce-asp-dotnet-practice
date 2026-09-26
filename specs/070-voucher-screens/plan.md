# Implementation Plan: Vouchers (part 2 - the screens)

**Branch**: `070-voucher-screens` | **Spec**: [spec.md](spec.md) | **Issue**: #108

## Design

Client only.

- **Types.**
  - `services/order/types.ts`: `AppliedVoucher`, `vouchers` on `Quote` and `Order`, `discount` on `OrderLine`,
    and `voucherCodes` on `CheckoutChoice`.
  - The new `services/voucher/`: a `Voucher` class with `create`, `mine` and `disable`. The model types
    `VoucherSummary` and `NewVoucher` live in `types.ts`.
- **Checkout.**
  - `components/checkout/voucher-box` holds an input, **Apply**, and the applied codes as removable chips.
  - Apply uses a mutation (`useTryVoucher`) that asks `Order.quote` with the codes plus the new one. Only when
    that succeeds does the code join the page's state.
  - The summary's own quote is keyed on the choice, codes included, so it follows by itself (research D1).
  - `usePlaceOrder` sends `voucherCodes`.
- **Showing it.**
  - `OrderTotals` lists `vouchers` (code, and the shop for a shop voucher, with the amount) in place of the
    single discount row. It falls back to that row when there are none.
  - `OrderLines` shows a line's discount.
- **Management.**
  - `components/voucher/voucher-page` is the list, the create dialog and disable. The seller's page and the
    administrator's page render it with `platform` true or false.
  - `components/voucher/voucher-form` builds the request. It offers free delivery and `NewCustomer` only when
    `platform` is true, and `FirstOrderInShop` only when it is false.
  - `utils/voucher/describe.ts` words a voucher: the benefit, the cap and the minimum, per currency.
  - The product picker is `components/voucher/product-picker`. It searches `useMyProducts` for a seller and
    `useProducts` for an administrator.
- **Routes:** `/shop/vouchers` and `/admin/vouchers`, with a menu entry in each console.
- **Words:** a new `vouchers` namespace, plus `checkout` for the box.

## Research

- **D1 - checking a code before keeping it.** The alternative was to add the code to state and let the
  summary's quote fail. Then one bad code would replace the whole summary with an error, and the customer
  would have to find and remove it. Asking first keeps the summary right and the error where the code was
  typed.
- **D2 - one form for both roles.** The rules differ in only three places:
  - free delivery is the platform's;
  - `NewCustomer` belongs to the platform;
  - `FirstOrderInShop` belongs to a shop.

  The server refuses the rest on its own, and the page shows its words.
- **D3 - products only, not variants, in the picker.** "20% off this lens" is the case people ask for. A
  variant-level voucher is possible through the API.
