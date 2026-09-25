# Tasks: Returning a delivered parcel (part 2 - the screens)

- [X] T001 Types, service calls and hooks for the three roles: `client/src/services/order/{types,index}.ts`, `client/src/services/admin/index.ts`, `client/src/hooks/{order,admin}/index.ts` and `client/src/constants/query-keys/index.ts`.
- [X] T002 [US1] [US2] [US3] The rules in `client/src/utils/order/returns.ts`, with tests in `returns.test.ts`, plus `RETURN_WINDOW_DAYS` in `client/src/constants/order`.
- [X] T003 [US1] `client/src/components/order/parcel-return`, wired into `pages/order` and `components/order/order-shipments`, with tests in `pages/order/index.test.tsx`.
- [X] T004 [US2] `client/src/components/order/return-decision`, wired into `pages/shop-sale`, with tests in `pages/shop-sale/index.test.tsx`.
- [X] T005 [US3] A returns card in `pages/admin-order`, and the new `client/src/pages/admin-returns` with its route and menu entry, all with tests.
- [X] T006 Words in `client/src/locales/{en,vi}/{orders,seller,admin}.json`, and "received" no longer claims it releases the payment.
- [X] T007 Lint, test, type-check and build. The round trip in the running storefront. Docs: returns page, timeline, backlog, counts.
