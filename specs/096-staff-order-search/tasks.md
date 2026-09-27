---
description: "Task list for Staff find any order"
---

# Tasks: Staff find any order

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included, written first - the server tests could not compile until the query existed.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Server (US1-US4)

- [X] T001 [US1] [US2] [US3] `StaffOrderSearchTests` (8) in `server/tests/Ecommerce.Order.Tests/`
- [X] T002 `GetOrdersForStaffQuery`, `StaffOrderSummaryResponse`, validator, handler
- [X] T003 `IOrderRepository.SearchForStaffAsync` and its implementation
- [X] T004 [US4] `GET /api/orders/staff`, `[Authorize(Roles = "Admin")]`

## Phase 2: Storefront (US1-US3)

- [X] T005 `Admin.findOrders`, types, `useStaffOrderSearch`, query key
- [X] T006 [US1] [US2] [US3] `/admin/orders/find` (id prefix or email, status tabs, names), route (Admin), menu entry, words en/vi
- [X] T007 Page tests (4)

## Phase 3: Verification and docs

- [X] T008 Mutations (quickstart Scenario 4) - each red
- [X] T009 [P] Bruno `order/` seq 12-13
- [X] T010 Rebuilt Order and the storefront; Bruno through the gateway
- [X] T011 Docs: `docs/features/fulfilment-and-delivery.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [X] T012 Merged as #203 (2026-09-27), closing #194
