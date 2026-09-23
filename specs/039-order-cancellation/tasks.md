# Tasks: Cancelling a paid order

## Phase 1 - Foundation

- [ ] T001 Contracts: `OrderCancelledEvent` in server/src/BuildingBlocks/Ecommerce.Contracts/Order/
- [ ] T002 Order: `Order.CancelledBy`, migration `AddOrderCancelledBy`; `Sales.Earning` split from `Sales.Statuses`

## Phase 2 - US1 customer cancels (P1)

- [ ] T003 [US1] Order tests first: cancel waiting order (status, event published once); preparing → 409 for customer; shipped → 409; not paid → 409; twice → no second event; not theirs → 404; cancel vs ship race - server/tests/Ecommerce.Order.Tests/CancellationTests.cs
- [ ] T004 [US1] `TryCancelAsync`, `CancelMyOrderCommand`, route `POST /api/orders/{id}/cancel`; `CancelledBy` on detail

## Phase 3 - US2 what it undoes (P1)

- [ ] T005 [US2] Inventory tests first: confirmed → stock back and Released; held → released; twice → once; announces - server/tests/Ecommerce.Inventory.Tests/RestockTests.cs + AnnouncementTests
- [ ] T006 [US2] Inventory: `RestockCancelledOrderCommand` + `RestockCancelledOrderConsumer`, registered
- [ ] T007 [US2] Payment tests first: refund recorded once with the payment's amount/currency; none for a rejected or missing payment - server/tests/Ecommerce.Payment.Tests/RefundTests.cs
- [ ] T008 [US2] Payment: `Refund`, `refunds` + migration, `RefundOrderCommand` + `RefundCancelledOrderConsumer`, refund on payment responses
- [ ] T009 [US2] Order tests: a cancelled order is in no balance, due list or payout; its seller sees it cancelled, cannot move it (409), other sellers 404

## Phase 4 - US3 staff (P2)

- [ ] T010 [US3] `CancelOrderCommand` + route `POST /api/orders/fulfilment/{id}/cancel`; tests: staff cancel while preparing, not once shipped

## Phase 5 - Client, end to end, docs

- [ ] T011 Client: cancel on the order page and the staff order page; Cancelled status everywhere; cancelled sale view; vi/en; Vitest tests
- [ ] T012 Bruno: cancel a shipped order → 409 (customer and staff); cancel without a token → 401; cancel someone else's → 404
- [ ] T013 verify-saga.sh: cancellation scenario (stock back, refund recorded)
- [ ] T014 Run everything; CLAUDE.md
