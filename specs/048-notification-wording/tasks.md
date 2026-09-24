# Tasks: Notification wording

- [X] T001 Declare every kind's data keys in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json`, embedded via `Ecommerce.Shared.csproj`
- [X] T002 `NotificationContract` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/NotificationContract.cs`: loads the declaration and reports a notice's problems
- [X] T003 [US2] Assert the kinds match `NotificationKind`, and that every captured notice matches the declaration, in `server/tests/Ecommerce.Order.Tests/NotificationTests.cs`, `server/tests/Ecommerce.Catalog.Tests/ProductReviewTests.cs`, `ReviewTests.cs`, `server/tests/Ecommerce.Identity.Tests/ShopApplicationTests.cs`, `ModerationTests.cs`
- [X] T004 [US1] [US2] Client tests first, against the declaration, in `client/src/utils/notifications/index.test.ts` (they fail before T005)
- [X] T005 [US1] Fill `product`, `reason` and `rating`, add the hole fallback, and make the English rating plural, in `client/src/utils/notifications/index.ts` and `client/src/locales/en/notifications.json`
- [X] T006 Mutation check: drop one pass-through, then one declared key on the server; the tests must go red
- [X] T007 Docs: `docs/features/audit-and-notifications.md`, `docs/project/timeline.md`, `docs/project/backlog.md`
