# Implementation Plan: Moderators, locks and bans

**Branch**: `043-moderators-and-locks` | **Spec**: [spec.md](spec.md)

## Technical Context

- **Identity domain.** Add `RoleNames.Moderator`; `DataInitializer` seeds it from `Descriptions`. `User`
  gains `LockedUntil`, `LockReason`, `BannedAt` and `BanReason`, all nullable. The migration only adds
  columns, so an earlier image still reads the table.
- **Shared.**
  - `Authentication/StaffRoles.Staff = "Admin,Moderator"`, for `[Authorize(Roles = StaffRoles.Staff)]`.
  - `Exceptions/ForbiddenException` → 403, with its message shown.
  - `NotificationKind.ModeratorGranted` and `ModeratorRevoked`.
- **Identity application** `Users/UserAdministration.cs`:
  - `GetUsersQuery(Search, Page, PageSize)`.
  - `GrantRoleCommand` and `RevokeRoleCommand` (`UserId`, `Role`).
  - `LockUserCommand(UserId, Days, Reason)` and `UnlockUserCommand`.
  - `BanUserCommand(UserId, Reason)` and `LiftBanCommand`.
  - `UserAdminResponse(Id, Email, FirstName, LastName, Roles, CreatedAt, LockedUntil, LockReason,
    BannedAt, BanReason)`.
  - Each write records an audit entry before its one save, and ends the user's sessions where the spec
    says so.
- **Sign-in** refuses a banned or locked account after the password check, with `ForbiddenException` and
  a `SignInRefused` audit entry. **Refresh** refuses both with the usual 401. Refresh already re-reads
  roles, so a grant arrives at the next refresh.
- **WebApi** `UsersController` at `api/users`. The class is `Staff`; grant, revoke, ban and lift are
  `Admin`. The target rules live in the handler, because an attribute cannot see the target.
- **Gateway**: `/api/users/**` → identity.
- **Client**:
  - `RequireRole` accepts several roles, and the console opens to Staff.
  - The sidebar shows each person the pages their role can use.
  - A moderator lands on Users.
  - `pages/admin-users` has search, a status badge, and a row menu of actions.
  - The notification wording covers the two new kinds.

## Research

- **D1 - Lock and ban are two columns, not a status enum.** A ban and a lock can overlap, and an older
  image must still parse the row. Two nullable pairs answer "why" and "until when" without either
  problem.
- **D2 - 403 at sign-in only after the right password.** Before the password is checked, a locked account
  and a wrong password must look the same (the #28 rule). After it, the person is who they say they are
  and deserves the reason.
- **D3 - Moderator is the only grantable role.** Admin would make a second bootstrap path. Seller has its
  own path (a shop application, #89). Customer is everybody.

## Constitution Check

| Principle | Verdict |
| :-- | :-- |
| I | Identity owns accounts and roles; the others read roles from the token. |
| III | The audit entry and the notification are published through the outbox, in the same save as the change. |
| IV | The acting staff member comes from the token. The target is an id in the route, and the rules about who may touch whom are checked against the stored row. |
| V | Integration tests for every rule, a mutation check on the 30-day cap and on the target guard, and Bruno for the round trip. |
