---
description: "Task list for In-app notifications"
---

# Tasks: In-app notifications

> Completed on 2026-09-27, after the feature merged (#94), from the code at that merge, the pull request and
> docs/features/audit-and-notifications.md.

**Input**: Design documents from `/specs/042-in-app-notifications/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included. Idempotency (one notice per id, one per event under redelivery) and the authorization
boundary (nobody reads another's inbox) are the categories the constitution's quality gates name as needing an
automated check, and the guarantees belong to the database, so they run against a real PostgreSQL.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 (people hear about what concerns them), US2 (the bell), US3 (the words follow the reader)

## The tasks as first recorded

The six tasks written while the feature was built. They are kept as they were; T007 onwards break them down
into what the merge actually contains.

- [X] T001 Contract + Shared `INotifier`, `NotificationKind`
- [X] T002 Activity: tests first (record once, own only, mark read, read-all, unread count, paging); entity, migration, consumer, queries, commands, controller
- [X] T003 Order: `TrySettleAsync` stage, `GetNoticeFactsAsync`, `OrderNotices`; tests for every event
- [X] T004 Gateway route; Bruno (own list 200, unread count, mark read, someone else's 404, anonymous 401)
- [X] T005 Client: bell, page, wording, polling; vi/en; tests
- [X] T006 Run everything; verify-saga; docs

---

## Phase 1: Setup (shared building blocks)

- [X] T007 [P] Add `UserNotificationRequested(NotificationId, RecipientId, Kind, Data, Link, OccurredAt)` in `server/src/BuildingBlocks/Ecommerce.Contracts/Activity/UserNotificationRequested.cs`
- [X] T008 [P] Add `INotifier`, `Notifier` (publishes through `IPublishEndpoint`, mints a v7 id), `NotificationKind` (eight constants) and `AddNotifier()` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs`

---

## Phase 2: Foundational (the inbox in Activity)

- [X] T009 [P] Create `Notification` in `server/src/Services/Activity/Ecommerce.Activity.Domain/Entities/Notification.cs`
- [X] T010 [P] Declare `INotificationRepository` (`TryAddAsync`, `GetPageAsync`, `CountUnreadAsync`, `TryMarkReadAsync`, `MarkAllReadAsync`) in `server/src/Services/Activity/Ecommerce.Activity.Application/Common/Interfaces/INotificationRepository.cs`
- [X] T011 Create `NotificationConfiguration` (table `notifications`, `ValueGeneratedNever()`, `Kind` 64, `Data` `jsonb`, `Link` 256, index `(RecipientId, CreatedAt)`, partial index `IX_notifications_unread`) in `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`, and add `Notifications` to `ActivityDbContext.cs`
- [X] T012 Generate migration `20260923195702_AddNotifications` in `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Migrations/`
- [X] T013 Implement `NotificationRepository` in `server/src/Services/Activity/Ecommerce.Activity.Infrastructure/Persistence/Repositories/NotificationRepository.cs`: insert `ON CONFLICT ("Id") DO NOTHING`; every read and update with the recipient in its `WHERE`; mark-read guarded on `ReadAt IS NULL`; register it in `DependencyInjection.cs`
- [X] T014 Add `CurrentUser` (a settable `ICurrentUser`) and the repository to `server/tests/Ecommerce.Activity.Tests/ActivityTestFixture.cs`

---

## Phase 3: User Story 1 - People hear about what concerns them (P1)

**Goal**: Every event in the spec's US1 table reaches exactly the right people, once.

**Independent test**: `Ecommerce.Order.Tests.NotificationTests` and `Ecommerce.Activity.Tests.NotificationTests` (quickstart scenario 6).

### Tests for User Story 1

- [X] T015 [P] [US1] Write `A_notification_lands_in_its_recipient_s_inbox_once` (four concurrent deliveries, one row) in `server/tests/Ecommerce.Activity.Tests/NotificationTests.cs`
- [X] T016 [P] [US1] Write `A_paid_order_tells_its_buyer_and_each_seller_once` (redelivered outcome tells nobody twice), `A_failed_order_tells_its_buyer_and_no_seller`, `Shipping_tells_the_buyer_with_the_tracking_and_receiving_tells_the_seller`, `A_cancellation_tells_the_buyer_and_every_seller` and `A_payout_tells_its_seller_how_much` in `server/tests/Ecommerce.Order.Tests/NotificationTests.cs`; register `AddNotifier()` in `OrderTestFixture.cs`
- [X] T017 [US1] Write `Settling_inside_a_consumer_transaction_joins_it` in `server/tests/Ecommerce.Order.Tests/NotificationTests.cs`: open a transaction on the context the way MassTransit's consumer outbox does, then settle (research D6)

### Implementation for User Story 1

- [X] T018 [US1] Implement `RecordNotificationCommand` + handler in `server/src/Services/Activity/Ecommerce.Activity.Application/Notifications/NotificationFeatures.cs`, and `RecordNotificationConsumer` in `server/src/Services/Activity/Ecommerce.Activity.WebApi/Consumers/RecordNotificationConsumer.cs`, registered in `Program.cs`
- [X] T019 [US1] Add `stage` to `TrySettleAsync` and add `GetNoticeFactsAsync` in `server/src/Services/Order/Ecommerce.Order.Application/Common/Interfaces/IOrderRepository.cs`
- [X] T020 [US1] Implement both in `server/src/Services/Order/Ecommerce.Order.Infrastructure/Persistence/Repositories/OrderRepository.cs`: join `Database.CurrentTransaction` when one is open, else open one inside `CreateExecutionStrategy().ExecuteAsync`; run `stage` only when the settle affected a row; read the facts after `EnsureShipmentsAsync`
- [X] T021 [US1] Create `OrderNotices` and `OrderNoticeFacts` in `server/src/Services/Order/Ecommerce.Order.Application/Orders/Common/OrderNotices.cs` - one place deciding recipients, data and links; sellers distinct; nobody for the shop's own parcel
- [X] T022 [US1] Notify on paid (buyer + each seller) with an `OrderPaid` audit entry in `.../Orders/Commands/CompleteOrder/CompleteOrderCommandHandler.cs`, and on failed with an `OrderFailed` audit entry in `.../Orders/Commands/FailOrder/FailOrderCommandHandler.cs`, both inside the settle's `stage`
- [X] T023 [US1] Add `ParcelAudit.RecordMoveAsync` (audit entry, and the `ParcelShipped` notice when the part is shipped) in `.../Orders/Common/ParcelAudit.cs`, and pass `INotifier` through `.../Fulfilment/FulfilmentStep.cs`, `.../Fulfilment/ShipOrderCommandHandler.cs` and `.../SellerFulfilment/SellerFulfilmentCommands.cs`
- [X] T024 [US1] Notify the parcel's seller when the customer confirms receipt, in the `stage` of `TryConfirmDeliveryAsync`, in `.../Orders/Commands/ConfirmDelivery/DeliveryCommands.cs`
- [X] T025 [US1] Notify the buyer (with `by`) and every seller on cancellation, in `CancelStep`'s `stage`, in `.../Orders/Commands/CancelOrder/CancelOrderCommands.cs`
- [X] T026 [US1] Notify the seller of a recorded payout, in `TryRecordAsync`'s `stage`, in `.../Orders/Commands/RecordPayout/RecordPayoutCommand.cs`
- [X] T027 [US1] Register `AddNotifier()` in `server/src/Services/Order/Ecommerce.Order.WebApi/Program.cs`

**Checkpoint**: Order 174/174 and Activity 28/28 green (pull request #94).

---

## Phase 4: User Story 2 - The bell (P1)

**Goal**: A signed-in person sees how many notices are unread, reads them, and marks them read.

**Independent test**: Bruno `notifications/` and the client tests (quickstart scenarios 2-7).

### Tests for User Story 2

- [X] T028 [P] [US2] Write `Nobody_reads_or_marks_another_s_notifications`, `Marking_read_lowers_the_count_and_all_at_once_clears_it` and `The_inbox_is_newest_first_a_page_at_a_time` in `server/tests/Ecommerce.Activity.Tests/NotificationTests.cs`
- [X] T029 [P] [US2] Write the bell's tests (count shown; list loaded only when opened; a choice marks read and navigates; mark all) in `client/src/components/layout/notification-bell/index.test.tsx`
- [X] T030 [P] [US2] Write the page's test (all, then unread only on its tab) in `client/src/pages/notifications/index.test.tsx`, and the service's (the caller's own inbox, naming nobody) in `client/src/services/notifications/index.test.ts`

### Implementation for User Story 2

- [X] T031 [US2] Implement `GetMyNotificationsQuery` (+ validator: page >= 1, page size 1-50), `GetMyUnreadCountQuery`, `MarkNotificationReadCommand` (someone else's or none is `Notification not found.`) and `MarkAllNotificationsReadCommand` in `server/src/Services/Activity/Ecommerce.Activity.Application/Notifications/NotificationFeatures.cs`
- [X] T032 [US2] Implement `NotificationsController` (`[Authorize]`; list, `unread-count`, `{id:guid}/read`, `read-all`) in `server/src/Services/Activity/Ecommerce.Activity.WebApi/Controllers/NotificationsController.cs`
- [X] T033 [US2] Add `notifications-route` and `notifications-root-route` to `activity-cluster` in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`
- [X] T034 [P] [US2] Add the `Notifications` service class and types in `client/src/services/notifications/index.ts` and `types.ts`, and query keys in `client/src/constants/query-keys/index.ts`
- [X] T035 [US2] Add `useUnreadCount` (every `NOTIFICATION_POLL_MS`, not in the background), `useNotifications` and `useMarkRead` in `client/src/hooks/notifications/index.ts`, with `NOTIFICATION_POLL_MS = 30_000` in `client/src/constants/notifications/index.ts`
- [X] T036 [US2] Build `NotificationBell` (latest 8 when opened) in `client/src/components/layout/notification-bell/index.tsx` and place it in `client/src/components/layout/top-bar/index.tsx` for signed-in visitors only
- [X] T037 [US2] Build `NotificationsPage` (All / Unread tabs, `PAGE_SIZE`, `Pager`) in `client/src/pages/notifications/index.tsx`, routed at `/notifications` behind `RequireAuth` in `client/src/routes/index.tsx`
- [X] T038 [P] [US2] Add six requests in `bruno/notifications/` (told of paid and shipped; unread count; another person's is 404; mark one; mark all; nothing left unread) and `bruno/security-checks/notifications without a token is 401.yml`

**Checkpoint**: Bruno 132/132 requests, 211 tests (pull request #94).

---

## Phase 5: User Story 3 - The words follow the reader (P2)

**Goal**: Each notice reads as a sentence in the reader's current language.

**Independent test**: `client/src/utils/notifications/index.test.ts` (quickstart scenarios 6 and 10).

- [X] T039 [P] [US3] Write `describeNotification`'s tests (each kind worded from its data in each language; who cancelled in words; "the shop" for a parcel with no seller; an unknown kind shown generically) in `client/src/utils/notifications/index.test.ts`
- [X] T040 [US3] Implement `describeNotification` in `client/src/utils/notifications/index.ts`
- [X] T041 [P] [US3] Add the words in `client/src/locales/en/notifications.json` and `client/src/locales/vi/notifications.json`, register the namespace in `client/src/config/i18n/index.ts`, and add audit labels `OrderPaid` / `OrderFailed` to `client/src/locales/{en,vi}/admin.json`

**Checkpoint**: client 184 tests pass; lint, type-check and build clean (pull request #94).

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T042 Run `verify-saga.sh` against the running stack - which found every order left `Submitted` (`The connection is already in a transaction`), fixed by T020's join branch; passes (approve)
- [X] T043 Mutation checks, each red then restored: the paid notice to the wrong recipient; the owner filter removed from mark-read; the join branch disabled
- [X] T044 [P] Screenshots: the bell with 2 unread at 1360px; the page at 390px with no horizontal overflow
- [X] T045 [P] Update `CLAUDE.md` (the notifications paragraph and the consumer-transaction trap)
- [X] T046 Merge by pull request #94, "feat(shared): in-app notifications", closing #87

---

## Dependencies & Execution Order

- **Setup (Phase 1)** blocks everything: both sides need the contract and `INotifier`.
- **Foundational (Phase 2)** blocks US1's Activity half and all of US2.
- **US1 (Phase 3)**: Activity's record path (T018) and Order's senders (T019-T027) are independent of each
  other in code; T020 must precede T022, which uses the settle's `stage`.
- **US2 (Phase 4)**: server endpoints (T031-T033) before the client (T034-T037); Bruno (T038) needs US1's
  notices to exist.
- **US3 (Phase 5)**: needs the types from T034; otherwise independent of the bell.
- **Polish (Phase 6)**: T042 needs everything running; it is the task that found the bug the unit tests could
  not.

### Parallel Opportunities

- T007 and T008; T009 and T010.
- The test files of each story (T015/T016; T028/T029/T030; T039).
- The Order senders T023-T026 touch separate handler files, after T021.

## Notes

- 46 tasks: the 6 originally recorded, then 2 setup, 6 foundational, 13 for US1, 11 for US2, 3 for US3 and 5
  polish.
- Tests: 4 new in `Ecommerce.Activity.Tests/NotificationTests.cs`, 6 in `Ecommerce.Order.Tests/NotificationTests.cs`,
  9 in the four client test files.
- T006's "docs" was `CLAUDE.md`: the pull request changed nothing under `docs/`. The feature page,
  [docs/features/audit-and-notifications.md](../../docs/features/audit-and-notifications.md), was created
  afterwards by `f466a63` ("docs: a documentation set fit for the project report", 2026-09-24).
- The research, data model, contracts and quickstart were written on 2026-09-27, after the merge, to bring this
  record to the specs/001 standard; they are not tasks of the feature.
