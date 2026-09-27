---
description: "Task list for A mistyped tracking reference can be corrected"
---

# Tasks: A mistyped tracking reference can be corrected

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1)

- [ ] T001 `NotificationKind.TrackingCorrected` and `notification-kinds.json`
- [ ] T002 [US1] `TryCorrectTrackingAsync` under the order's lock: the guarded update, the summary, the stage
- [ ] T003 [US1] `CorrectSaleTrackingCommand` (Seller) and `CorrectShopTrackingCommand` (Admin), their validators, audit, notice; routes
- [ ] T004 `TrackingCorrectionTests`

## Phase 2: Storefront (US1)

- [ ] T005 Services, hooks, the `CorrectTracking` dialog on the sale and admin order pages; words en/vi; tests

## Phase 3: Verification and docs

- [ ] T006 Mutations (quickstart Scenario 3), each red; full Order and client suites
- [ ] T007 [P] Bruno; rebuilt Order and the storefront; post-design Constitution re-check
- [ ] T008 Docs: fulfilment, audit-and-notifications, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T009 Merged as #225, closing #212
