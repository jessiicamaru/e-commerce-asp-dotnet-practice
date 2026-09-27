---
description: "Task list for A mistyped tracking reference can be corrected"
---

# Tasks: A mistyped tracking reference can be corrected

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written with the code.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1)

- [X] T001 `NotificationKind.TrackingCorrected` and `notification-kinds.json`
- [X] T002 [US1] `TryCorrectTrackingAsync` under the order's lock: the guarded update, the summary, the stage
- [X] T003 [US1] `CorrectSaleTrackingCommand` (Seller) and `CorrectShopTrackingCommand` (Admin), their validators, audit, notice; routes
- [X] T004 `TrackingCorrectionTests`

## Phase 2: Storefront (US1)

- [X] T005 Services, hooks, the `CorrectTracking` dialog on the sale and admin order pages; words en/vi; tests

## Phase 3: Verification and docs

- [X] T006 Mutations (quickstart Scenario 3, plus two client ones), each red; Order 302, client 531
- [X] T007 [P] Bruno 321/321 (3 new); rebuilt Order and the storefront; post-design Constitution re-check: no violations. The ship hint no longer says a reference cannot be changed
- [X] T008 Docs: fulfilment, audit-and-notifications, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [x] T009 Merged as #225, closing #212
