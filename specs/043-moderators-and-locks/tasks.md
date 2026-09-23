# Tasks: Moderators, locks and bans

- [X] T001 Shared: `StaffRoles`, `ForbiddenException` (403, shown), notification kinds
- [X] T002 Identity: `Moderator` role, lock/ban columns + migration
- [X] T003 Identity: tests first (`ModerationTests`) - grant/revoke + refresh, lock refuses sign-in and refresh, unlock, 30-day cap, target rules, ban admin-only, audit + notice
- [X] T004 Identity: `UserAdministration` queries/commands, sign-in and refresh refusals, `UsersController`
- [X] T005 Gateway route; Bruno (search, grant, refresh shows role, lock → sign-in 403, unlock, revoke; 403s for customer and moderator)
- [X] T006 Client: console for Staff, role-filtered sidebar, Users page, wording; tests
- [X] T007 Run everything; verify-saga; docs
