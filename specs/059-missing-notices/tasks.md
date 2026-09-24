# Tasks: The notices nobody got

- [X] T001 [US3] Declare the four kinds: `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs`, `notification-kinds.json`
- [X] T002 [US1] [US2] Server tests first: `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs` (lock, ban), `server/tests/Ecommerce.Order.Tests/NotificationTests.cs` (the sweep), `server/tests/Ecommerce.Catalog.Tests/ReviewTests.cs` (hide)
- [X] T003 [US1] [US2] Notices in `UserAdministration.cs`, `DeliveryCommands.cs` + `OrderNotices.cs`, `ReviewFeatures.cs`
- [X] T004 [US3] Storefront words and `until` formatting: `client/src/utils/notifications/index.ts`, `client/src/locales/{en,vi}/notifications.json`, the two callers; the contract test fails first
- [X] T005 Mutation checks; docs `docs/features/audit-and-notifications.md`, `docs/project/*`
