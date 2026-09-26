---
description: "Task list for Vouchers (part 2 - the screens)"
---

# Tasks: Vouchers (part 2 - the screens)

> Completed on 2026-09-27, after the feature merged (#154), from the code at that merge, the pull request and
> docs/features/vouchers.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/README.md](contracts/README.md)

**Tests**: Included - a change under `client/` ships with tests.

## Format: `[ID] [P?] [Story] Description`

## As first recorded

- [X] T001 Types, the `Voucher` service, and hooks. `Order.quote` and `Order.place` carry `voucherCodes`. URL tests.
- [X] T002 [US1] `components/checkout/voucher-box` and `useTryVoucher`, wired into `pages/checkout`. `OrderTotals` and `OrderLines` show the vouchers and the discounts. Tests.
- [X] T003 [US2] [US3] `utils/voucher/describe` (with tests), and the `voucher-form`, `product-picker` and `voucher-page` components.
- [X] T004 [US2] [US3] `pages/shop-vouchers` and `pages/admin-vouchers`, their routes and their menu entries. Tests for each role.
- [X] T005 Words (vi and en, the `vouchers` namespace). Then lint, test, type-check and build, the live check through the storefront, and the docs.

## The same work by file (added in the backfill; all done in #154)

- [X] T006 [P] `client/src/services/voucher/{index.ts, types.ts}` and `index.test.ts` (routes, no owner, repeated `voucherCodes`)
- [X] T007 [P] `AppliedVoucher`, `vouchers`, `discount`, `voucherCodes` in `client/src/services/order/types.ts`; the repeated parameter in `client/src/services/order/index.ts`
- [X] T008 [P] `useMyVouchers`, `useCreateVoucher`, `useDisableVoucher`, `useTryVoucher` in `client/src/hooks/voucher/index.ts`; `myVouchers` in `client/src/constants/query-keys/index.ts`
- [X] T009 [US1] `client/src/components/checkout/voucher-box/index.tsx`; `client/src/pages/checkout/index.tsx` and its 3 new tests
- [X] T010 [US1] `client/src/components/order/order-totals/index.tsx` (vouchers in place of the single discount row) and `order-lines/index.tsx` (a line's discount); `pages/order` test
- [X] T011 [US2] [US3] `client/src/components/voucher/voucher-form/to-new-voucher.ts` with `to-new-voucher.test.ts` (5)
- [X] T012 [US2] [US3] `client/src/components/voucher/{voucher-form, product-picker, voucher-page}/index.tsx`; `client/src/components/ui/checkbox.tsx` via `shadcn add`
- [X] T013 [US2] [US3] `client/src/utils/voucher/describe.ts` with `describe.test.ts` (5)
- [X] T014 [US2] [US3] `client/src/pages/{shop-vouchers, admin-vouchers}/index.tsx`; `pages/shop-vouchers/index.test.tsx` (seller 4, admin 1); routes in `client/src/routes/index.tsx`; menu entries in `layouts/seller-layout` and `layouts/admin-layout` (`adminOnly`, with its test)
- [X] T015 [P] Words: `client/src/locales/{en,vi}/vouchers.json` (new namespace, registered in `client/src/config/i18n/index.ts`), `checkout.json`, `seller.json`, `admin.json`
- [X] T016 Mutation checks - 8 of 8 caught, each reverted with `git checkout` (table in the PR)
- [X] T017 Live: storefront rebuilt; bundle holds the routes and words; `/shop/vouchers`, `/admin/vouchers`, `/checkout` deep-link 200; Bruno through the storefront 229/229, 376/376
- [X] T018 [P] Docs: "Where it happens in the storefront" in `docs/features/vouchers.md`, timeline row, #108 to Fixed in the backlog, counts (storefront 399), CLAUDE.md
- [X] T019 Merged as #154 on 2026-09-26 (`8d864ea`), closing #108

## Dependencies

T006-T008 before the components (T009-T014); words alongside; checks and docs last.

## Implementation notes

- Not done: clicking through in a real browser (stated in the PR).
