---
description: "Task list for Moderators, locks and bans"
---

# Tasks: Moderators, locks and bans

> Completed on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/](contracts/)

**Tests**: included, and written first for the server (T003): the guarantees are rules about who may do
what to whom and whether a stopped account really cannot get in, which only the real sign-in and refresh
commands against a real PostgreSQL can show (Principle V).

**Organization**: T001-T007 are the original tasks, kept with their words; file paths were added on
2026-09-27. T008-T024 were added the same day from the pull request and the diff: the work they describe
was part of #95 but had no task line. Numbers are therefore not in phase order.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependencies)
- **[Story]**: US1 grant and revoke, US2 lock and ban, US3 the record, US4 the console

---

## Phase 1: Shared building blocks

- [X] T001 [P] Shared: `StaffRoles`, `ForbiddenException` (403, shown), notification kinds - `server/src/BuildingBlocks/Ecommerce.Shared/Authentication/StaffRoles.cs`, `server/src/BuildingBlocks/Ecommerce.Shared/Exceptions/ForbiddenException.cs`, `NotificationKind.ModeratorGranted` / `ModeratorRevoked` in `server/src/BuildingBlocks/Ecommerce.Shared/Notifications/Notifier.cs`
- [X] T008 Map `ForbiddenException` to 403 "Forbidden" and add it to the exceptions whose message is shown in every environment, in `server/src/BuildingBlocks/Ecommerce.Shared/Middlewares/GlobalExceptionHandler.cs`

## Phase 2: Identity foundation

- [X] T002 Identity: `Moderator` role, lock/ban columns + migration - `RoleNames.Moderator` and its description in `server/src/Services/Identity/Ecommerce.Identity.Domain/Constants/RoleNames.cs` (seeded by `DataInitializer` from `Descriptions`); `LockedUntil`, `LockReason`, `BannedAt`, `BanReason`, `IsLocked(now)`, `IsBanned` in `server/src/Services/Identity/Ecommerce.Identity.Domain/Entities/User.cs`; `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Migrations/20260923202324_AddAccountLocks.cs`
- [X] T009 [P] Reasons at most 500 characters in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Configurations/UserConfigurations.cs`
- [X] T010 [P] `GetByIdAsync` (tracked, with roles) and `SearchAsync` (email or name, newest first, paged) in `server/src/Services/Identity/Ecommerce.Identity.Application/Common/Interfaces/IUserRepository.cs` and `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs`
- [X] T011 [P] Let a test's caller hold roles - `IdentityTestFixture.For(userId, roles)`, `FixedUser.IsInRole` reading them, and `AddNotifier()` - in `server/tests/Ecommerce.Identity.Tests/IdentityTestFixture.cs`
- [X] T012 [P] Insert `EmailCaseTests`' old-schema rows in SQL rather than through today's model, which now has columns that schema lacks, in `server/tests/Ecommerce.Identity.Tests/EmailCaseTests.cs`

## Phase 3: Tests first (US1, US2, US3)

- [X] T003 [US1] [US2] [US3] Identity: tests first (`ModerationTests`) - grant/revoke + refresh, lock refuses sign-in and refresh, unlock, 30-day cap, target rules, ban admin-only, audit + notice - `server/tests/Ecommerce.Identity.Tests/ModerationTests.cs` (8 tests, including `Moderator_is_the_only_role_that_can_be_granted` and `Staff_find_people_by_part_of_their_email`)

## Phase 4: Identity behaviour (US1, US2, US3)

- [X] T004 [US1] [US2] [US3] Identity: `UserAdministration` queries/commands, sign-in and refresh refusals, `UsersController` - `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`, `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Login/LoginCommandHandler.cs`, `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Refresh/RefreshTokenCommandHandler.cs`, `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/UsersController.cs`
- [X] T013 [US2] `ModerationRules` (`ModeratorMaxLockDays = 30`, `AdminMaxLockDays = 365`, `EnsureMayStop`) called by both the lock and the ban handler, the cap checked before the target is read, in `server/src/Services/Identity/Ecommerce.Identity.Application/Users/UserAdministration.cs`
- [X] T014 [US2] End every session after a lock or ban with `RevokeAllRefreshTokensAsync`, and refuse a stopped account at refresh whatever its token, in `UserAdministration.cs` and `RefreshTokenCommandHandler.cs`
- [X] T015 [US1] Refuse a grant to a banned account with 409 in `UserAdministration.cs`
- [X] T016 [US1] [US3] Register `AddNotifier()` in `server/src/Services/Identity/Ecommerce.Identity.WebApi/Program.cs`, so a grant and a revoke can tell the person

## Phase 5: Gateway and Bruno

- [X] T005 [US1] [US2] [US3] Gateway route; Bruno (search, grant, refresh shows role, lock → sign-in 403, unlock, revoke; 403s for customer and moderator) - `users-route` and `users-root-route` in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`; the 15 requests of `bruno/admin-users/` (folder `seq: 12`), ending with `the audit log records the lock and the unlock.yml`
- [X] T017 [P] `bruno/security-checks/people without a token is 401.yml`, and `bruno/security-checks/folder.yml` moved from `seq: 12` to `13` so `admin-users` runs before it

## Phase 6: Storefront (US4)

- [X] T006 [US4] Client: console for Staff, role-filtered sidebar, Users page, wording; tests - `client/src/components/auth/require-role/index.tsx` (any of several roles), `client/src/routes/index.tsx` (`/admin` for `['Admin', 'Moderator']`, `/admin/users`), `client/src/layouts/admin-layout/index.tsx` (`adminOnly` links filtered), `client/src/pages/admin-users/index.tsx`, `client/src/locales/{en,vi}/admin.json`, `client/src/locales/{en,vi}/notifications.json`; tests in `client/src/pages/admin-users/index.test.tsx`, `client/src/layouts/admin-layout/index.test.tsx`, `client/src/components/auth/require-role/index.test.tsx`
- [X] T018 [P] [US4] `client/src/services/accounts/index.ts` (the `Accounts` class over `/users`) and `client/src/services/accounts/types.ts` (`Account`, `AccountPage`, `MODERATOR_MAX_LOCK_DAYS = 30`)
- [X] T019 [P] [US4] `client/src/hooks/accounts/index.ts` (`useAccounts`, `useAccountActions`, each action re-reading the list) and `queryKeys.accounts` in `client/src/constants/query-keys/index.ts`
- [X] T020 [US4] `client/src/pages/admin-home/index.tsx`: an administrator sees the fulfilment queue, a moderator is sent to `/admin/users`
- [X] T021 [US4] `client/src/pages/admin-users/stop-dialog.tsx`: lengths 1, 3, 7, 14, 30, 90, 365, only those up to 30 for a moderator; a reason required, at most 500
- [X] T022 [P] [US4] `isStaff` on the auth context in `client/src/context/auth/index.tsx` and `types.ts`; the console entry in `client/src/components/layout/top-bar/index.tsx` and `client/src/components/layout/user-menu/index.tsx` drawn for `isStaff` instead of `isAdmin`; `renderAsModerator` in `client/src/test/render.tsx`; neighbouring tests brought along (`client/src/components/layout/user-menu/index.test.tsx`, `client/src/pages/product/index.test.tsx`, `client/src/pages/shop-sales/index.test.tsx`)

## Phase 7: Verification and docs

- [X] T007 Run everything; verify-saga; docs
- [X] T023 Mutation checks, each red and then restored: the cap raised to 1000 days (`A_moderator_locks_for_at_most_thirty_days...` failed); the moderator-on-moderator guard removed (`Nobody_stops_themselves_or_an_administrator...` failed); the sign-in refusal removed (the lock test and the ban test failed); the sidebar filter removed (the layout test failed). Screenshots of `/admin/users` at 1360px and at 390px with no horizontal overflow
- [X] T024 `CLAUDE.md` records Staff, the grantable role, the lock limits, the target rules, the sign-in refusal after the right password and `ForbiddenException`. Merged as #95 on 2026-09-23 (UTC), closing #88

---

## Dependencies & Execution Order

- **Phase 1** blocks everything: the controller's attribute and the handlers' exceptions come from it.
- **Phase 2** blocks the tests and the behaviour: the columns, the role and the repository methods.
- **Phase 3** before **Phase 4**: the tests were written first.
- **Phase 5** needs Phase 4 running behind the gateway.
- **Phase 6** needs only the contract; it could run beside Phase 4.
- **Phase 7** last.

## Verification recorded in #95

- Identity tests **65/65**, including the 8 new `ModerationTests`, against real PostgreSQL.
- Client **195/195**; lint, type-check and build clean.
- Bruno **148/148 requests, 238 tests**, through the gateway.
- `verify-saga.sh` passes.
- The four mutation checks in T023, each red.

## Notes

- T008-T024 were added on 2026-09-27; T001-T007 keep their numbers and words.
- 24 tasks: 7 original, 17 added from the pull request.
