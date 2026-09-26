---
description: "Task list for Each seller ships their own part"
---

# Tasks: Each seller ships their own part

> Completed on 2026-09-27, after the feature merged (#79), from the code at that merge, the pull request and
> docs/features/fulfilment-and-delivery.md. Story labels were added to the existing tasks; T019 onward
> were added in this backfill.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel; **[Story]**: US1 seller moves, US2 address, US3 customer's parcels,
  US4 the shop's part, US5 storefront

**Tests**: requested - every acceptance item is a refusal or a race, and those are only proven by a test that fails without its guard.

## Phase 1: Foundational - parts exist

- [X] T001 [US1] `ShipmentStatus` + `OrderShipment` in server/src/Services/Order/Ecommerce.Order.Domain/; `Order.Shipments` navigation
- [X] T002 [US1] Configuration with the NULLS NOT DISTINCT unique index in server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Configurations/OrderShipmentConfiguration.cs
- [X] T003 [US1] Migration `AddOrderShipments` with the backfill SQL (server/src/Services/Order/Ecommerce.Order.Infrastructure/Migrations/20260923093713_AddOrderShipments.cs)
- [X] T004 [US1] Checkout stages one part per distinct seller - server/src/Services/Order/Ecommerce.Order.Application/Orders/Commands/SubmitOrder/SubmitOrderCommandHandler.cs

## Phase 2: US1 + US4 - moving a part (P1)

- [X] T005 [US1] [US4] Tests first in server/tests/Ecommerce.Order.Tests/ShipmentTests.cs: a seller prepares and ships their part; repeating is a no-op; skipping and going back are refused; another seller's part and the shop's are 404; an unpaid or failed order is 404; two parts shipped concurrently leave the order Shipped; parts missing on an order written by an older version are created in the order's state
- [X] T006 [US1] [US4] `TryMoveShipmentAsync` + `EnsureShipmentsAsync` on IOrderRepository / OrderRepository: order row lock, create-missing, guarded part update, summary recompute - one transaction
- [X] T007 [US1] `PrepareMySaleCommand`, `ShipMySaleCommand` (+ validator) in Application/Orders/Commands/SellerFulfilment/
- [X] T008 [US4] Admin commands move the shop's part (FulfilmentStep); 409 when the order has no shop part; queue reads the shop's part
- [X] T009 [US1] Routes `POST /api/orders/sales/{id}/preparing` and `/shipment`, Seller only

## Phase 3: US2 + US3 - what each side reads (P1)

- [X] T010 [US2] [US3] Tests: sale status/tracking from the seller's part; address while Pending/Preparing and not once Shipped; order detail lists parts; summary counts parts shipped; a one-part order reads as before
- [X] T011 [US2] [US3] Sale reads, order detail `shipments`, summary `shipmentCount`/`shipmentsShipped`

## Phase 4: US5 - storefront (P2)

- [X] T012 [P] [US5] Services/hooks for the two seller moves; types for shipments
- [X] T013 [US5] Seller sale page: next-step button, tracking dialog, address while needed
- [X] T014 [US5] Customer order page parcels; orders list "1 of 2 shipped"
- [X] T015 [US5] Strings (vi, en) and Vitest tests

## Phase 5: Polish

- [X] T016 [P] [US1] Bruno: seller moves a part that is not theirs → 404; customer → 403; anonymous → 401
- [X] T017 Run everything; mutation check on the guards; end to end on Minh's two-seller order; verify-saga.sh
- [X] T018 CLAUDE.md

> **What "tests first" actually was here.** The code was written before `ShipmentTests`, so the first
> green run proved nothing. A mutation run removed seven guards one at a time - the order row lock, the
> seller on the part guard, the address cut-off, "shipped when ALL parts are", the paid check, the
> order's state for missing parts, the single-parcel tracking copy - and **every one turned at least one
> test red**. The lock's removal is caught by the concurrency test, which is the one that matters.
>
> **Found on the way, not planned:** the first version of the checkout test sorted two v7 Guids made
> in the same millisecond and failed 3 runs in 6 on untouched code; it now compares sets. And the
> client suite, now 104 tests, hit Vitest's 5s timeout on different form-typing tests on different
> runs ("Test timed out", never an assertion); `testTimeout` is 15s, with the reason in the config.

## Added in the 2026-09-27 backfill (work the pull request shows)

- [X] T019 [US1] `ShipmentMoveOutcome` / `ShipmentMoveResult` in server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/ShipmentMove.cs - Moved, AlreadyThere, WrongState, OrderNotPaid, NoSuchPart
- [X] T020 [US1] The move runs through `CreateExecutionStrategy().ExecuteAsync` in OrderRepository.cs, because production's `EnableRetryOnFailure` refuses a caller-opened transaction and the test fixture does not enable it - without this the tests pass and production throws
- [X] T021 [US4] `TryAdvanceAsync` removed from IOrderRepository / OrderRepository; the staff steps go through `TryMoveShipmentAsync(orderId, sellerId: null, ...)`
- [X] T022 [P] [US3] `OrderMapping.Describe(ShipmentStatus)` reads a waiting part as `Paid`; `ToShipments` orders the shop's part first, then sellers by id
- [X] T023 [P] `OrderShipmentConfiguration` sets `Id` `ValueGeneratedNever()` so a part staged through `Order.Shipments` is inserted, not updated into a concurrency exception
- [X] T024 [P] [US5] `components/order/order-shipments` (with its test) and `components/seller/sale-actions` in client/src/components/
- [X] T025 [P] Bruno: `seller/a seller cannot ship a part that is not theirs`, `security-checks/a customer cannot ship a sale is 403`, `security-checks/preparing a sale without a token is 401`
- [X] T026 `client/vitest.config.ts` `testTimeout` 15 s, with the reason in the config: at 104 tests a different form-typing test hit the 5 s default on each run
- [X] T027 The checkout test compares sets of seller ids instead of sorting two v7 Guids minted in the same millisecond (it failed 3 runs in 6 on untouched code)
- [X] T028 The design record completed to the specs/001 standard: quickstart.md, plan structure, research labels, the data model's transitions and lock order, contract responses as built (2026-09-27)
- [X] T029 Merged as **#79** (`ccea3d6`) on 2026-09-23, closing #76: Order 98 tests, all backend 349, client 104; Bruno 103/103 requests, 164/164 tests; `verify-saga.sh` unchanged and green; end to end 19/19 on demo data; seven guards mutated, each caught

### The mutation run, as the pull request recorded it

| Guard removed | Caught by |
| :-- | :-- |
| order row lock (`FOR UPDATE`) | `Two_parts_shipped_at_the_same_moment_leave_the_order_shipped` |
| seller in the part guard | 10 tests, including "not theirs is not found" |
| address cut-off after shipping | `A_seller_sees_the_address_until_their_part_is_shipped_and_not_after` |
| "Shipped when **all** parts are" | 4 tests |
| order must be paid | `A_part_of_an_unpaid_order_cannot_be_started` plus an existing fulfilment test |
| missing parts start in the order's state | `Parts_missing_from_an_older_order_…` plus 2 existing fulfilment tests |
| one parcel's tracking copied to the order | `An_order_in_one_parcel_…` plus 2 existing fulfilment tests |
