---
description: "Task list for A seller is told when a variant runs low"
---

# Tasks: A seller is told when a variant runs low

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: each test is written first and seen red before its code.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The rule and the column (US1, US2)

- [X] T001 `StockItem.LowStockThreshold`, configuration with CHECK, migration `AddLowStockThreshold`
- [X] T002 `LowStock` options (`Inventory:LowStock:DefaultThreshold`, validated at startup) and the pure `Crossed(before, after, threshold)`
- [X] T003 `StockRanLowEvent` in Contracts

## Phase 2: Inventory announces (US1)

- [X] T004 [US1] `LowStockTests` first (red): 6 → 4 publishes once; 4 → 3 none; back to 6 then 6 → 4 again; threshold 0 never; override; set-on-hand none; redelivery none
- [X] T005 [US1] `ReserveStockCommandHandler`: before and after under the lock, stage the event before the save

## Phase 3: The threshold (US2)

- [X] T006 [US2] Tests first: set, clear to default, 0, out of range 400, another seller's 404
- [X] T007 [US2] `SetLowStockThresholdCommand` and its validator and handler (`StockOwnership`), the route, `StockResponse` fields

## Phase 4: Catalog tells the seller (US1)

- [X] T008 [US1] `LowStockNoticeTests` first: seller notified with product, variant and count left (conforms to the kinds file); shop's own none; unknown variant none
- [X] T009 [US1] `NotificationKind.StockRunningLow`, `notification-kinds.json` (kind and `left` placeholder); `NotifyLowStockCommand`; `StockRanLowConsumer` registered

## Phase 5: Storefront (US2)

- [X] T010 [US2] Tests first: the editor shows the effective threshold and sends a value or a clear
- [X] T011 [US2] `Stock.setLowStockThreshold`, hook, the field in `variant-editor`; words in seller and notifications, en and vi

## Phase 6: Verification and docs

- [X] T012 Mutations (quickstart Scenario 4), each red but one equivalent (recorded); Inventory 75, Catalog 241, Activity 40, Order 286, client 516
- [X] T013 [P] Bruno: seller sets and reads the threshold; another seller's 404; 401. Rebuilt Inventory, Catalog, the gateway and the storefront; Bruno 314/314; `verify-saga.sh` passes; post-design Constitution re-check: no violations
- [X] T014 Docs: `docs/features/marketplace.md`, `docs/features/audit-and-notifications.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T015 Merged as #209, closing #200
