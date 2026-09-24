# Tasks: Audit gaps and the misleading reuse warning

- [ ] T001 [US1] [US2] Tests first: `server/tests/Ecommerce.Catalog.Tests/AuditTests.cs` (category translation set/remove, product translation removed), `server/tests/Ecommerce.Identity.Tests/AuditTests.cs` (sign-out, default address, reuse), `server/tests/Ecommerce.Identity.Tests/RefreshTokenReuseTests.cs` (a stale tab after an unlock)
- [ ] T002 [US1] Catalog handlers: `Categories/Translations/SetCategoryTranslationCommand.cs`, `Products/Translations/SetProductTranslationCommand.cs`
- [ ] T003 [US1] [US2] Identity handlers: `Addresses/Commands/SetDefaultAddress/`, `Auth/Commands/Logout/`, `Auth/Commands/Refresh/`
- [ ] T004 Mutation checks; docs `docs/features/audit-and-notifications.md`, `docs/features/auth/*`, `docs/project/*`
