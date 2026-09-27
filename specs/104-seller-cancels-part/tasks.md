---
description: "Task list for A seller cancels the part of an order they cannot fulfil"
---

# Tasks: A seller cancels the part of an order they cannot fulfil

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code of each phase.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Contracts and columns

- [X] T001 `OrderPartCancelledEvent`; `OrderShipment` cancellation columns and migration; `Refund.PartId` and migration
- [X] T002 `NotificationKind.PartCancelled` and `notification-kinds.json`

## Phase 2: Order (US1, US2, US3)

- [X] T003 [US1] `TryCancelPartAsync` under the order's lock: guarded part update, last-part to whole cancel, the stage
- [X] T004 [US1] Cancelled parts excluded: summary, move guard, whole-order cancel checks, `Earning`, insights
- [X] T005 [US1] `CancelSalePartCommand` (Seller) and `CancelShopPartCommand` (Admin): refund amount, event, vouchers, audit, notice; routes
- [X] T006 Part responses carry the cancellation
- [X] T007 `PartCancellationTests`

## Phase 3: Inventory and Payment (US1, US2)

- [X] T008 [US1] Restock filtered to variants; `RestockCancelledPartConsumer`; `RestockCancelledPartTests`
- [X] T009 [US1] [US2] Part refund, remainder for the whole; `RefundCancelledPartConsumer`; `PartRefundTests`

## Phase 4: Storefront

- [X] T010 Seller's sale page: cancel with a reason; admin order page: the shop's part; buyer's order page: the cancelled part; words en/vi; tests

## Phase 5: Verification and docs

- [X] T011 Mutations (quickstart Scenario 4), eleven, each red; Order 299, Inventory 78, Payment 28, Activity 40, client 526
- [X] T012 [P] Bruno 318/318 (4 new); rebuilt Inventory and Payment, then Order and the storefront; both new queues bound; `verify-saga.sh`; the live check (quickstart Scenario 5); post-design Constitution re-check: no violations
- [X] T013 Docs: fulfilment, marketplace, audit-and-notifications, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T014 Merged as #224, closing #211
