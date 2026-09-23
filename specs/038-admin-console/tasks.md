# Tasks: An administrator's console

- [ ] T001 Order test first: staff read any order with its parts; a missing order is 404 - server/tests/Ecommerce.Order.Tests/FulfilmentTests.cs
- [ ] T002 Order: `GetOrderForStaffQuery` + route `GET /api/orders/fulfilment/{id}` (Admin)
- [ ] T003 Bruno: staff read an order (200), a customer is 403, a seller is 403
- [ ] T004 Client: `isAdmin`; `services/admin`, `hooks/admin`, query keys; `admin` locale namespace (vi/en)
- [ ] T005 Client: move `SaleActions` to `components/order/parcel-actions` taking status + mutations; seller sale page uses it
- [ ] T006 [US1] Client: `admin-layout`, `/admin` queue (tabs per state, `Pager`), `/admin/orders/:id` (shop parcel, address, all parcels, actions)
- [ ] T007 [US2] Client: `/admin/payouts` - due list, AlertDialog confirm, pay, toast with the recorded amount, 409 in server words
- [ ] T008 [US3] Client: header link and user-menu entry for administrators only; routes under `RequireRole role="Admin"`
- [ ] T009 Vitest tests for every page and the guard; mutation checks
- [ ] T010 Run everything; drive the console in a browser; CLAUDE.md
