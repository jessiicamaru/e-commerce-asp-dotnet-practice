---
description: "Task list for notification wording"
---

# Tasks: Notification wording

> Completed on 2026-09-27, after the feature merged (#129), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/)

**Tests**: the feature is mostly tests - the defect was that nothing tested the storefront against what the
server sends.

## Format: `[ID] [P?] [Story] Description`

T001-T007 are the tasks as written during the work, kept as they were. T008 onward record the remaining
steps visible in the merge; all were done in #129.

## Original tasks

- [X] T001 Declare every kind's data keys in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json`, embedded via `Ecommerce.Shared.csproj`
- [X] T002 `NotificationContract` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/NotificationContract.cs`: loads the declaration and reports a notice's problems
- [X] T003 [US2] Assert the kinds match `NotificationKind`, and that every captured notice matches the declaration, in `server/tests/Ecommerce.Order.Tests/NotificationTests.cs`, `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs`, `ReviewTests.cs`, `server/tests/Ecommerce.Identity.Tests/ShopApplicationTests.cs`, `ModerationTests.cs`
- [X] T004 [US1] [US2] Client tests first, against the declaration, in `client/src/utils/notifications/index.test.ts` (they fail before T005)
- [X] T005 [US1] Fill `product`, `reason` and `rating`, add the hole fallback, and make the English rating plural, in `client/src/utils/notifications/index.ts` and `client/src/locales/en/notifications.json`
- [X] T006 Mutation check: drop one pass-through, then one declared key on the server; the tests must go red
- [X] T007 Docs: `docs/features/audit-and-notifications.md`, `docs/project/timeline.md`, `docs/project/backlog.md`

## Added in the completion (2026-09-27)

- [X] T008 [P] [US2] Publish and assert the three kinds no test had published before: `ModeratorRevoked` in `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs`, `ShopRejected` in `server/tests/Ecommerce.Identity.Tests/ShopApplicationTests.cs`, `ProductTakenDown` in `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs`
- [X] T009 [US1] Default a missing `shop` to "the shop" only for `ParcelShipped` in `client/src/utils/notifications/index.ts` (research D5)
- [X] T010 [US2] A sample per declared key, with how it must show, in `client/src/utils/notifications/index.test.ts`, so a new key fails with `no sample for "<key>"`
- [X] T011 [P] Update the notification paragraph in `CLAUDE.md` to point at `notification-kinds.json`
- [X] T012 Run lint, `tsc -b`, `vite build` and the client suite (271/271 on three consecutive runs after one uncaptured failure on the first); run the affected server classes against PostgreSQL (Order 7/7, Catalog 12/12, Identity 14/14); the five mutations in [quickstart.md](quickstart.md) Scenario 4 each went red; merged as PR #129 (closes #119)
