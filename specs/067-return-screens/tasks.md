---
description: "Task list for Returning a delivered parcel (part 2 - the screens)"
---

# Tasks: Returning a delivered parcel (part 2 - the screens)

> Completed on 2026-09-27, after the feature merged (#151), from the code at that merge, the pull request and
> docs/features/returns.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/README.md](contracts/README.md)

**Tests**: Included - a change under `client/` ships with tests (CLAUDE.md).

## Format: `[ID] [P?] [Story] Description`

## As first recorded

- [X] T001 Types, service calls and hooks for the three roles: `client/src/services/order/{types,index}.ts`, `client/src/services/admin/index.ts`, `client/src/hooks/{order,admin}/index.ts` and `client/src/constants/query-keys/index.ts`.
- [X] T002 [US1] [US2] [US3] The rules in `client/src/utils/order/returns.ts`, with tests in `returns.test.ts`, plus `RETURN_WINDOW_DAYS` in `client/src/constants/order`.
- [X] T003 [US1] `client/src/components/order/parcel-return`, wired into `pages/order` and `components/order/order-shipments`, with tests in `pages/order/index.test.tsx`.
- [X] T004 [US2] `client/src/components/order/return-decision`, wired into `pages/shop-sale`, with tests in `pages/shop-sale/index.test.tsx`.
- [X] T005 [US3] A returns card in `pages/admin-order`, and the new `client/src/pages/admin-returns` with its route and menu entry, all with tests.
- [X] T006 Words in `client/src/locales/{en,vi}/{orders,seller,admin}.json`, and "received" no longer claims it releases the payment.
- [X] T007 Lint, test, type-check and build. The round trip in the running storefront. Docs: returns page, timeline, backlog, counts.

## Added in the backfill (all done in #151)

- [X] T008 [P] The shared dialog `client/src/components/shared/text-prompt/index.tsx` - no empty text, closes only on success, shows a refusal inside (research D4)
- [X] T009 [P] [US3] `RETURN_QUEUE_STATES` and the `adminReturns` query key in `client/src/services/admin/types.ts` and `client/src/constants/query-keys/index.ts`
- [X] T010 [P] [US3] `StaffReturn` in `client/src/pages/admin-order/staff-return.tsx`; route `returns` under `/admin` in `client/src/routes/index.tsx`; the `adminOnly` menu entry in `client/src/layouts/admin-layout/index.tsx`, with its test
- [X] T011 [P] Route tests for the ten new calls in `client/src/services/{order,admin}/index.test.ts`
- [X] T012 Mutation checks - 9 of 9 caught, each reverted with `git checkout` (table in the PR)
- [X] T013 Bruno through the rebuilt storefront container (`baseUrl=http://localhost:8088`): 215/215 requests, 352/352 tests; the bundle checked for the new routes and words; `/admin/returns` deep link 200
- [X] T014 [P] Docs: "Where it happens in the storefront" in `docs/features/returns.md`, timeline row 067, #107 to Fixed in the backlog, counts (storefront tests 371), CLAUDE.md; reference regenerated with no change
- [X] T015 Merged as #151 on 2026-09-25 (`f3eaa91`), closing #107

## Dependencies

T001 and T002 before the components (T003-T005, T008-T010); words (T006) alongside; checks and docs (T007, T011-T014)
last.

## Implementation notes

- Not done: clicking through in a real browser (stated in the PR).
