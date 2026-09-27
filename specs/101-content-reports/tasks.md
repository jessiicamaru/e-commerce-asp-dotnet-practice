---
description: "Task list for Shoppers report a review, a question or a product"
---

# Tasks: Shoppers report a review, a question or a product

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: included. They were written with the handlers, because they could not compile before the report types existed.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Storage (US1)

- [X] T001 `ContentReport` and its enums; configuration with the partial unique index; migration `AddContentReports`
- [X] T002 `ContentReportRepository`: `TryAddAsync` (`ON CONFLICT DO NOTHING`), `CloseAsync` (guarded `UPDATE ... RETURNING`), `GetQueueAsync`, `InTransactionAsync`

## Phase 2: Reporting and the queue (US1, US2)

- [X] T003 [US1] `ReportContentCommand`: validator, visibility and own-content checks, 409 on a second open report
- [X] T004 [US2] `GetReportQueueQuery` (grouped, most reported first, with excerpts) and `DismissReportsCommand` (notices and audit)
- [X] T005 [US2] `ContentReports.CloseAsync` in the hide review, hide question, hide answer and take-down stage callbacks
- [X] T006 `ReportsController`; gateway routes; `ReportActioned` and `ReportDismissed` in `NotificationKind` and `notification-kinds.json`
- [X] T007 `ContentReportTests` (5)

## Phase 3: Storefront (US1, US2)

- [X] T008 [US1] `services/reports`, `hooks/reports`, `ReportButton` on reviews, questions and the product page; words en and vi
- [X] T009 [US2] `/admin/reports` (act through the existing hide or take-down, or no action), route and menu; words en and vi; notice words
- [X] T010 Tests: `ReportButton` (3), the page (4)

## Phase 4: Verification and docs

- [X] T011 Mutations (quickstart Scenario 4), each red; Catalog 239, client 511
- [X] T012 [P] Bruno `reviews/` seq 12-20, `security-checks/` seq 64-65
- [X] T013 Rebuilt Catalog, the gateway and the storefront; Bruno 309/309
- [X] T014 Docs: `docs/features/moderation-and-staff.md`, `ratings-and-reviews.md`, `product-questions.md`, `audit-and-notifications.md`, CLAUDE.md, backlog, timeline; `generate_reference.py`
- [ ] T015 Merged as #208, closing #199
