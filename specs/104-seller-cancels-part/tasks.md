---
description: "Task list for A seller cancels the part of an order they cannot fulfil"
---

# Tasks: A seller cancels the part of an order they cannot fulfil

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Contracts and columns

- [ ] T001 `OrderPartCancelledEvent`; `OrderShipment` cancellation columns and migration; `Refund.PartId` and migration
- [ ] T002 `NotificationKind.PartCancelled` and `notification-kinds.json`

## Phase 2: Order (US1, US2, US3)

- [ ] T003 [US1] `TryCancelPartAsync` under the order's lock: guarded part update, last-part to whole cancel, the stage
- [ ] T004 [US1] Cancelled parts excluded: summary, move guard, whole-order cancel checks, `Earning`, insights
- [ ] T005 [US1] `CancelSalePartCommand` (Seller) and `CancelShopPartCommand` (Admin): refund amount, event, vouchers, audit, notice; routes
- [ ] T006 Part responses carry the cancellation
- [ ] T007 `PartCancellationTests`

## Phase 3: Inventory and Payment (US1, US2)

- [ ] T008 [US1] Restock filtered to variants; `RestockCancelledPartConsumer`; `RestockCancelledPartTests`
- [ ] T009 [US1] [US2] Part refund, remainder for the whole; `RefundCancelledPartConsumer`; `PartRefundTests`

## Phase 4: Storefront

- [ ] T010 Seller's sale page: cancel with a reason; admin order page: the shop's part; buyer's order page: the cancelled part; words en/vi; tests

## Phase 5: Verification and docs

- [ ] T011 Mutations (quickstart Scenario 4), each red; full Order, Inventory, Payment, Catalog, client suites
- [ ] T012 [P] Bruno; rebuilt Inventory, Payment, Order, storefront; `verify-saga.sh`; post-design Constitution re-check
- [ ] T013 Docs: fulfilment, marketplace, audit-and-notifications, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T014 Merged as #224, closing #211
