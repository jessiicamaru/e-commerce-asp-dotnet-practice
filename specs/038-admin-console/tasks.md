---
description: "Task list for An administrator's console"
---

# Tasks: An administrator's console

> Completed on 2026-09-27, after the feature merged (#82, #83), from the code at that merge, the pull
> requests and docs/features/fulfilment-and-delivery.md. Story labels and paths were added to T001-T005;
> T011 onward were added in this backfill.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel; **[Story]**: US1 the shop's parcels, US2 payouts, US3 only staff

- [X] T001 [US1] Order test first: staff read any order with its parts; a missing order is 404 - server/tests/Ecommerce.Order.Tests/FulfilmentTests.cs
- [X] T002 [US1] Order: `GetOrderForStaffQuery` + route `GET /api/orders/fulfilment/{id}` (Admin) - server/src/Services/Order/Ecommerce.Order.Application/Orders/Queries/GetOrderForStaff/GetOrderForStaffQuery.cs, OrdersController.cs
- [X] T003 [P] [US3] Bruno: staff read an order (200), a customer is 403, a seller is 403
- [X] T004 [US1] [US2] Client: `isAdmin`; `services/admin`, `hooks/admin`, query keys; `admin` locale namespace (vi/en)
- [X] T005 [US1] Client: move `SaleActions` to `components/order/parcel-actions` taking status + mutations; seller sale page uses it
- [X] T006 [US1] Client: `admin-layout`, `/admin` queue (tabs per state, `Pager`), `/admin/orders/:id` (shop parcel, address, all parcels, actions)
- [X] T007 [US2] Client: `/admin/payouts` - due list, AlertDialog confirm, pay, toast with the recorded amount, 409 in server words
- [X] T008 [US3] Client: header link and user-menu entry for administrators only; routes under `RequireRole role="Admin"`
- [X] T009 Vitest tests for every page and the guard; mutation checks
- [X] T010 Run everything; drive the console in a browser; CLAUDE.md

> The two server tests were seen RED against a stub handler first. The client code preceded its tests;
> seven mutations (owner-scoped read, any parcel taken for the shop's, the queue's default state,
> paying without confirming, the dialog left open, the menu drawn for everyone, an amount sent) each
> turned a test red. The payouts test found a real defect on the way: the confirm dialog stayed open
> after "Record", hiding the refusal behind it. Driven in a browser as the administrator: a mixed order
> (the shop's Canon, Tuấn's battery) prepared and shipped from the console with its tracking reference,
> Tuấn's parcel left for him; Tuấn paid out from the payouts page (toast: 1.716.000 ₫).

## Added in the 2026-09-27 backfill (work the pull requests show)

- [X] T011 [P] [US1] `shopParcelOf` in client/src/pages/admin-order/shop-parcel.ts with its own test - the `isShop` part, `null` when there is none, the whole order when there are no parts
- [X] T012 [P] Test helpers client/src/test/refusal.ts and render.tsx (a server refusal in its own words; rendering as a given role)
- [X] T013 Merged as **#82** (`c6f5f84`) on 2026-09-23: Order 140, client 143 tests; Bruno 114/114 requests, 182/182 tests; `verify-saga.sh` green; no overflow at 390 px
- [X] T014 [US2] Follow-up: `AlertDialogAction` made a `Close` in client/src/components/ui/alert-dialog.tsx - the fourth edited file under `ui/`, recorded in client/README.md and CLAUDE.md; `admin-payouts` stops controlling its dialog by hand
- [X] T015 [P] Follow-up: `address-card` test `closes the dialog once confirmed`, red before the fix
- [X] T016 [P] Follow-up: the seller console's four tabs as equal columns on a phone (client/src/layouts/seller-layout/index.tsx); the seller payouts test's first wait raised to 5 s after one timeout under parallel load
- [X] T017 The design record completed to the specs/001 standard: research.md, data-model.md, quickstart.md, plan structure (2026-09-27)
- [X] T018 Follow-up merged as **#83** (`f68b4d1`) on 2026-09-23: client 145 tests, lint clean, build passing; screenshots at 390 px and 1360 px
