# Implementation Plan: Vouchers (part 2 - the screens)

> Completed on 2026-09-27, after the feature merged (#154), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Branch**: `070-voucher-screens` | **Spec**: [spec.md](spec.md) | **Issue**: #108 | **PR**: #154 (merged 2026-09-26)

## Summary

The storefront half of vouchers: a voucher box at checkout that tries each code with the server before keeping it,
the vouchers and line discounts on the summary and the order page, and one voucher page with one form serving both
`/shop/vouchers` and `/admin/vouchers`. No server change.

## Technical Context

**Language/Version**: TypeScript, React 19

**Primary Dependencies**: Vite, Tailwind v4, shadcn/ui (the `checkbox` component added with `shadcn add`, unedited),
axios, TanStack Query, react-i18next

**Storage**: none

**Testing**: Vitest with jsdom and Testing Library; oxlint; `tsc -b`; `vite build`

**Target Platform**: the storefront, through the gateway

**Constraints**: client only; words in Vietnamese and English; `PAGE_SIZE` = 12 for the voucher list

**Scale/Scope**: one new service, four hooks, five components, two pages

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

(From the code: the request itself is built by a pure function beside the form,
`components/voucher/voucher-form/to-new-voucher.ts`, which is what the form tests exercise; the `vouchers` namespace is
registered in `client/src/config/i18n/index.ts`.)

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

In the standard shape, with alternatives: [research.md](research.md).

## Constitution Check

The plan as first written had no Constitution Check; this one was added in the backfill, against all five principles
of [constitution.md](../../.specify/memory/constitution.md).

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** The client computes no discount: every amount it shows is the server's quote or the order's frozen values. It copies the server's role rules only to decide which options to offer (D2) |
| **II. Clean Architecture Layering** | **Pass (not applicable to the server).** No server code changed. The client keeps its own layering: `services/voucher` over axios, `hooks/voucher` for TanStack Query, `components/voucher` composed by `pages/shop-vouchers` and `pages/admin-vouchers` |
| **III. Atomic Writes and Idempotent Messaging** | **Pass (not applicable).** No write path changed; the claim of a voucher stays in the order's transaction on the server |
| **IV. Identity Comes From the Token** | **Pass.** No request names an owner - `services/voucher/index.test.ts` asserts it ("naming no owner"); the server reads whose from the token |
| **V. Evidence Over Assumption** | **Pass, with one gap stated.** Vitest 399/399 (21 new), 8 of 8 mutations caught, Bruno 229/229 through the rebuilt storefront including the checkout claiming a voucher, deep links 200. **Not done: clicking through in a real browser** |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/070-voucher-screens/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── contracts/README.md       # the endpoints relied on; none changed
└── checklists/requirements.md
```

### Source Code (touched at the merge)

```text
client/src/
├── services/voucher/{index.ts, types.ts, index.test.ts}          # new
├── services/order/{index.ts, types.ts}
├── hooks/voucher/index.ts                                       # useMyVouchers, useCreateVoucher, useDisableVoucher, useTryVoucher
├── components/checkout/voucher-box/index.tsx                    # new
├── components/order/{order-totals, order-lines}/index.tsx
├── components/voucher/{voucher-page, voucher-form, product-picker}/  # new; voucher-form/to-new-voucher.ts (+ test)
├── components/ui/checkbox.tsx                                   # shadcn add, unedited
├── utils/voucher/{describe.ts, describe.test.ts}
├── pages/{shop-vouchers, admin-vouchers}/index.tsx, pages/shop-vouchers/index.test.tsx
├── pages/checkout/{index.tsx, index.test.tsx}, pages/order/index.test.tsx
├── layouts/{seller-layout, admin-layout}/index.tsx, layouts/admin-layout/index.test.tsx
├── routes/index.tsx, constants/query-keys/index.ts, config/i18n/index.ts
└── locales/{en,vi}/{vouchers,checkout,seller,admin}.json
```

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| - | - | - |

## What this feature does not finish

- No list of the vouchers a customer could use; no editing; no category or variant targets from the screens.
- No real-browser click-through at merge.
