# Tasks: An administrator's console

- [X] T001 Order test first: staff read any order with its parts; a missing order is 404 - server/tests/Ecommerce.Order.Tests/FulfilmentTests.cs
- [X] T002 Order: `GetOrderForStaffQuery` + route `GET /api/orders/fulfilment/{id}` (Admin)
- [X] T003 Bruno: staff read an order (200), a customer is 403, a seller is 403
- [X] T004 Client: `isAdmin`; `services/admin`, `hooks/admin`, query keys; `admin` locale namespace (vi/en)
- [X] T005 Client: move `SaleActions` to `components/order/parcel-actions` taking status + mutations; seller sale page uses it
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
