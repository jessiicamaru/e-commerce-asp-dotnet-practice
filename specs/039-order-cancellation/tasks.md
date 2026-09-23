# Tasks: Cancelling a paid order

## Phase 1 - Foundation

- [X] T001 Contracts: `OrderCancelledEvent` in server/src/BuildingBlocks/Ecommerce.Contracts/Order/
- [X] T002 Order: `Order.CancelledBy`, migration `AddOrderCancelledBy`; `Sales.Earning` split from `Sales.Statuses`

## Phase 2 - US1 customer cancels (P1)

- [X] T003 [US1] Order tests first: cancel waiting order (status, event published once); preparing → 409 for customer; shipped → 409; not paid → 409; twice → no second event; not theirs → 404; cancel vs ship race - server/tests/Ecommerce.Order.Tests/CancellationTests.cs
- [X] T004 [US1] `TryCancelAsync`, `CancelMyOrderCommand`, route `POST /api/orders/{id}/cancel`; `CancelledBy` on detail

## Phase 3 - US2 what it undoes (P1)

- [X] T005 [US2] Inventory tests first: confirmed → stock back and Released; held → released; twice → once; announces - server/tests/Ecommerce.Inventory.Tests/RestockTests.cs + AnnouncementTests
- [X] T006 [US2] Inventory: `RestockCancelledOrderCommand` + `RestockCancelledOrderConsumer`, registered
- [X] T007 [US2] Payment tests first: refund recorded once with the payment's amount/currency; none for a rejected or missing payment - server/tests/Ecommerce.Payment.Tests/RefundTests.cs
- [X] T008 [US2] Payment: `Refund`, `refunds` + migration, `RefundOrderCommand` + `RefundCancelledOrderConsumer`, refund on payment responses
- [X] T009 [US2] Order tests: a cancelled order is in no balance, due list or payout; its seller sees it cancelled, cannot move it (409), other sellers 404

## Phase 4 - US3 staff (P2)

- [X] T010 [US3] `CancelOrderCommand` + route `POST /api/orders/fulfilment/{id}/cancel`; tests: staff cancel while preparing, not once shipped

## Phase 5 - Client, end to end, docs

- [X] T011 Client: cancel on the order page and the staff order page; Cancelled status everywhere; cancelled sale view; vi/en; Vitest tests
- [X] T012 Bruno: cancel a shipped order → 409 (customer and staff); cancel without a token → 401; cancel someone else's → 404
- [X] T013 verify-saga.sh: cancellation scenario (stock back, refund recorded)
- [X] T014 Run everything; CLAUDE.md

> **Honest ordering.** Order's code was written before `CancellationTests`; the Inventory and Payment tests
> were written first and seen RED against no-op handlers (5 and 2 red). So every guard was then removed
> one at a time: Order 10/10 (row lock, customer-while-preparing, shipped, owner, repeat, not paid,
> balance and payout counting cancelled, cancelled told to a stranger, address on a cancelled sale),
> Inventory 3/3, Payment 3/3, client 6/6 - each turned a test red. Two did not at first and the tests were
> fixed: concurrent refunds never actually raced (now a repository that never sees an existing refund
> forces the unique violation, and the test saves the context again as the EF outbox does), and a client
> assertion looked for "parcels sent" where the heading says "parcels shipped".
> End to end: verify-saga.sh cancels a second order - stock back to 47/0, refund 1,155,000 of 1,155,000,
> a repeat 200; the two queues each have one consumer; and Lan cancelled an order in the browser.
