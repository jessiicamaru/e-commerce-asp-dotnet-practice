---
description: "Task list for A seller is told when a variant runs low"
---

# Tasks: A seller is told when a variant runs low

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: each test is written first and seen red before its code.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: The rule and the column (US1, US2)

- [ ] T001 `StockItem.LowStockThreshold`, configuration with CHECK, migration `AddLowStockThreshold`
- [ ] T002 `LowStock` options (`Inventory:LowStock:DefaultThreshold`, validated at startup) and the pure `Crossed(before, after, threshold)`
- [ ] T003 `StockRanLowEvent` in Contracts

## Phase 2: Inventory announces (US1)

- [ ] T004 [US1] `LowStockTests` first (red): 6 → 4 publishes once; 4 → 3 none; back to 6 then 6 → 4 again; threshold 0 never; override; set-on-hand none; redelivery none
- [ ] T005 [US1] `ReserveStockCommandHandler`: before and after under the lock, stage the event before the save

## Phase 3: The threshold (US2)

- [ ] T006 [US2] Tests first: set, clear to default, 0, out of range 400, another seller's 404
- [ ] T007 [US2] `SetLowStockThresholdCommand` and its validator and handler (`StockOwnership`), the route, `StockResponse` fields

## Phase 4: Catalog tells the seller (US1)

- [ ] T008 [US1] `LowStockNoticeTests` first: seller notified with product, variant and count left (conforms to the kinds file); shop's own none; unknown variant none
- [ ] T009 [US1] `NotificationKind.StockRunningLow`, `notification-kinds.json` (kind and `left` placeholder); `NotifyLowStockCommand`; `StockRanLowConsumer` registered

## Phase 5: Storefront (US2)

- [ ] T010 [US2] Tests first: the editor shows the effective threshold and sends a value or a clear
- [ ] T011 [US2] `Stock.setLowStockThreshold`, hook, the field in `variant-editor`; words in seller and notifications, en and vi

## Phase 6: Verification and docs

- [ ] T012 Mutations (quickstart Scenario 4), each red; full Inventory, Catalog and client suites
- [ ] T013 [P] Bruno: seller sets and reads the threshold; another seller's 404; 401. Rebuilt Inventory, Catalog and the storefront; Bruno run; post-design Constitution re-check
- [ ] T014 Docs: `docs/features/marketplace.md`, `docs/features/audit-and-notifications.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T015 Merged as #209, closing #200
