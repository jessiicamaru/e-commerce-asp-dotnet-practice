# Implementation Plan: Moderators, locks and bans

> Completed on 2026-09-27, after the feature merged (#95), from the code at that merge, the pull request and
> docs/features/moderation-and-staff.md.

**Branch**: `043-moderators-and-locks` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md) | **Issue**: #88

## Summary

Give the shop a second kind of staff and a way to stop an account, all inside Identity, which already owns
accounts and roles. A `Moderator` role is seeded beside the other three; `users` gains two nullable pairs
of columns for a lock and a ban; a new `UsersController` at `/api/users` lets staff search people, lets an
administrator grant and revoke Moderator and ban, and lets any member of staff lock and unlock. The rules
about **whom** a caller may stop depend on the target's row, so they live in `ModerationRules`, not in an
attribute. Sign-in refuses a stopped account with 403 and the reason - only after the right password -
and refresh refuses it with the ordinary 401 whatever token it presents. Every action records an audit
entry (and a grant or revoke a notification) through the outbox in the same save as the change.
`Ecommerce.Shared` gains the two pieces the following moderation features need in every service:
`StaffRoles.Staff` and `ForbiddenException`. The storefront's `/admin` console opens to moderators, draws
each role only its pages, and gains a Users page.

## Technical Context

**Language/Version**: C# on .NET 10; TypeScript with React 19 in `client/`

**Primary Dependencies**: MediatR, FluentValidation, EF Core with Npgsql, MassTransit (outbox, and the test
harness in tests), `Ecommerce.Shared` (`AddJwtAuthentication`, `IAuditTrail`, `INotifier`,
`GlobalExceptionHandler`), YARP at the gateway; in the client TanStack Query, react-i18next and shadcn/ui

**Storage**: PostgreSQL 16, Identity's `ecommerce_identity_db` on host port 5435 - four new nullable columns
on `users`; the `Moderator` row in `roles` is seeded at startup, not by the migration

**Testing**: xUnit against a real PostgreSQL through `IdentityTestFixture` (`ModerationTests`); Vitest with
Testing Library in the client; Bruno through the gateway (`bruno/admin-users/`)

**Target Platform**: Identity on 5056 behind the gateway on 5000; the storefront through Vite's proxy

**Project Type**: a change to an existing Clean Architecture service, a shared building block, the gateway
and the storefront

**Performance Goals**: none stated. No measurement was recorded

**Constraints**: the migration only adds nullable columns, so an earlier Identity image still reads
`users`; a locked account must look like any other before the password is checked (#28); the acting staff
member comes from the token, never the request

**Scale/Scope**: a handful of staff; people searched a page (12, at most 50) at a time

### Design as it was planned

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

### What the code at the merge adds to that design

- `UserAdministration.cs` holds the query, the six commands, their validators, `ModerationRules`
  (`ModeratorMaxLockDays = 30`, `AdminMaxLockDays = 365`, `EnsureMayStop`) and one handler class,
  `UserAdministrationHandlers`, implementing all six command handlers. The file is one per subject rather
  than the `Commands/<UseCase>/` folders the constitution names - see Complexity Tracking.
- The paged result is `UserAdminPage(Items, Page, PageSize, TotalCount)`. `UserAdminResponse.From` shows a
  lock that has run out as no lock (`LockedUntil` and `LockReason` null).
- `IUserRepository` gains `GetByIdAsync` (tracked, with roles) and `SearchAsync` (lower-cased `Contains`
  on the email and on `FirstName + " " + LastName`, newest first, then by id).
- A lock or ban saves the account and its audit entry, **then** calls the existing
  `RevokeAllRefreshTokensAsync` - a separate `ExecuteUpdate` after the save. Refresh's own check of the row
  is what makes that ordering safe (research D5).
- A grant refuses a banned account with 409.
- The gateway got two routes, `users-route` (`/api/users/{**catch-all}`) and `users-root-route`
  (`/api/users`), following the pairing the other routes already used.
- `Program.cs` registers `AddNotifier()` in Identity, which had not needed it before.
- The client gained `pages/admin-home` (an administrator sees the fulfilment queue, a moderator is
  redirected to `/admin/users`), `services/accounts`, `hooks/accounts`, `isStaff` on the auth context, and
  `pages/admin-users/stop-dialog.tsx` with preset lengths 1, 3, 7, 14, 30, 90 and 365 days.
- `IdentityTestFixture.For(userId, roles)` lets a test's caller hold roles; `EmailCaseTests` inserts its
  old-schema rows in SQL, because today's model has columns that schema lacks.

## Research

Full decisions with rejected alternatives are in [research.md](research.md). In short:

- **D1 - Lock and ban are two columns, not a status enum.** A ban and a lock can overlap, and an older
  image must still parse the row. Two nullable pairs answer "why" and "until when" without either
  problem.
- **D2 - 403 at sign-in only after the right password.** Before the password is checked, a locked account
  and a wrong password must look the same (the #28 rule). After it, the person is who they say they are
  and deserves the reason.
- **D3 - Moderator is the only grantable role.** Admin would make a second bootstrap path. Seller has its
  own path (a shop application, #89). Customer is everybody.

D4 to D11 cover where the target rules live, how sessions end, the lock limits, the shared Staff name,
`ForbiddenException`, 409 versus 403, the console, and search.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md). The original plan's table
named principles I, III, IV and V; its sentences are kept below and II is added.

| Principle | Assessment |
| :-- | :-- |
| **I. Service Autonomy** | **Pass.** Identity owns accounts and roles; the others read roles from the token. Nothing here reads another service's database; the lock and ban are Identity's facts and no copy of them exists elsewhere. `StaffRoles` and `ForbiddenException` go into `Ecommerce.Shared`, which is the one place cross-cutting infrastructure may be shared |
| **II. Clean Architecture Layering** | **Pass, with one deviation recorded.** Domain gains columns and two pure predicates (`IsLocked(now)`, `IsBanned`); Application declares the two new repository methods in `Common/Interfaces/IUserRepository.cs`; Infrastructure implements them; the controller only dispatches. The deviation is folder shape: the query and six commands share `Users/UserAdministration.cs` instead of one folder per use case (Complexity Tracking) |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The audit entry and the notification are published through the outbox, in the same save as the change. Repeats are no-ops by guarded checks: a grant of a role held, a revoke of one not held, an unlock of an unlocked account and a lift of an absent ban write nothing and publish nothing. The session revocation after a lock or ban is a second statement outside that save; it is safe because refresh re-reads the row and refuses a stopped account on its own (research D5) |
| **IV. Identity Comes From the Token** | **Pass.** The acting staff member comes from the token. The target is an id in the route, and the rules about who may touch whom are checked against the stored row. No command carries the caller's id; `ModerationRules.EnsureMayStop` takes `ICurrentUser`. Moderator is granted only by an administrator through an `[Authorize(Roles = RoleNames.Admin)]` endpoint - out of band, never by self-registration |
| **V. Evidence Over Assumption** | **Pass.** Integration tests for every rule, a mutation check on the 30-day cap and on the target guard, and Bruno for the round trip. `ModerationTests` runs the handlers against a real PostgreSQL, with sign-in and refresh as the real commands rather than a stubbed check. #95 records four mutations, each turning a test red: the cap raised to 1000 days, the moderator-on-moderator guard removed, the sign-in refusal removed (both the lock and the ban test failed), and the sidebar filter removed (the layout test failed) |

**Post-design re-check**: no violation of a principle's intent. Two things are worth naming rather than
waving through: the session revocation is not in the change's transaction (III - judged safe because the
row, not the token table, is what refresh decides on), and the use-case folders were collapsed into one
file (II - recorded below). The unlock path had no target rules at this merge; that is a gap in the
feature's own rules (spec, Out of scope), not a principle violation, and specs/050 closed it.

## Project Structure

### Documentation (this feature)

```text
specs/043-moderators-and-locks/
├── spec.md                  # Feature specification
├── plan.md                  # This file
├── research.md              # D1-D11 with rejected alternatives
├── data-model.md            # users columns, migration, derived account states
├── contracts/
│   ├── http-api.md          # /api/users endpoints, sign-in and refresh changes, gateway routes
│   └── messages.md          # the audit entries and notifications this feature publishes
├── quickstart.md            # validation scenarios through the gateway, tests, Bruno, SQL
├── checklists/
│   └── requirements.md      # spec quality checklist
└── tasks.md                 # the task list, all done
```

### Source Code (repository root)

```text
server/src/BuildingBlocks/Ecommerce.Shared/
├── Authentication/StaffRoles.cs                      # new: Admin, Moderator, Staff = "Admin,Moderator"
├── Exceptions/ForbiddenException.cs                  # new: 403, message shown
├── Middlewares/GlobalExceptionHandler.cs             # maps ForbiddenException to 403 and shows its message
└── Notifications/Notifier.cs                         # NotificationKind.ModeratorGranted, ModeratorRevoked

server/src/Services/Identity/
├── Ecommerce.Identity.Domain/
│   ├── Constants/RoleNames.cs                        # Moderator, with its description
│   └── Entities/User.cs                              # LockedUntil, LockReason, BannedAt, BanReason, IsLocked, IsBanned
├── Ecommerce.Identity.Application/
│   ├── Auth/Commands/Login/LoginCommandHandler.cs    # 403 after the right password, SignInRefused
│   ├── Auth/Commands/Refresh/RefreshTokenCommandHandler.cs  # 401 for a stopped account
│   ├── Common/Interfaces/IUserRepository.cs          # GetByIdAsync, SearchAsync
│   └── Users/UserAdministration.cs                   # new: query, commands, validators, ModerationRules, handlers
├── Ecommerce.Identity.Infrastructure/
│   ├── Configurations/UserConfigurations.cs          # reasons max 500
│   ├── Migrations/20260923202324_AddAccountLocks.cs  # new: four nullable columns
│   └── Persistence/Repositories/UserRepository.cs    # GetByIdAsync, SearchAsync
└── Ecommerce.Identity.WebApi/
    ├── Controllers/UsersController.cs                # new: /api/users
    └── Program.cs                                    # AddNotifier()

server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json   # users-route, users-root-route

server/tests/Ecommerce.Identity.Tests/
├── ModerationTests.cs                                # new: 8 tests
├── IdentityTestFixture.cs                            # For(userId, roles), AddNotifier
└── EmailCaseTests.cs                                 # old-schema rows inserted in SQL

client/src/
├── components/auth/require-role/                     # any of several roles (+ tests)
├── context/auth/                                     # isStaff
├── layouts/admin-layout/                             # role-filtered sidebar (+ new test)
├── pages/admin-home/index.tsx                        # new: where /admin opens, per role
├── pages/admin-users/                                # new: index.tsx, stop-dialog.tsx, index.test.tsx
├── services/accounts/                                # new: Accounts class and types
├── hooks/accounts/index.ts                           # new: useAccounts, useAccountActions
├── routes/index.tsx                                  # /admin for Admin or Moderator, /admin/users
└── locales/{en,vi}/{admin,notifications}.json        # Users page and the two notification kinds

bruno/admin-users/                                    # new folder, 15 requests, seq 12
bruno/security-checks/people without a token is 401.yml   # new; the folder moved to seq 13
```

**Structure Decision**: everything server-side lives in Identity, which already owns accounts, roles and
sessions; the two shared pieces go to `Ecommerce.Shared` because the next three features (#89, #90, #91)
need them in Identity and Catalog. The client follows `client/README.md`: a folder per page, a service
class and a hooks module per entity.

## Complexity Tracking

| Violation | Why needed | Simpler alternative rejected because |
| :-- | :-- | :-- |
| The query and six commands share `Users/UserAdministration.cs` rather than `Users/Commands/<UseCase>/` folders (Principle II's folder convention) | Not recorded. The six commands share one target lookup, one snapshot shape and one set of rules; one handler class implements all six | Not recorded. Recorded here on 2026-09-27 because the convention exists and the code departs from it |

## What this feature does not finish

- **An access token already issued lives out its 15 minutes.** A lock ends sessions at the next refresh,
  not mid-request. Accepted at the time; specs/065 (#112) later made a stop reach a signed-in session
  within seconds.
- **Unlocking has no target rules.** Any member of staff could unlock any account: a moderator could lift
  an administrator's year-long lock or another moderator's, or - with an access token that outlived the
  lock - their own. Specs/050 (#121) added `ModerationRules.EnsureMayRelease`.
- **A re-lock replaces the old end date**, shorter or longer, with the same target rules as a first lock.
  The page does not offer it (a locked row shows Unlock, not Lock), but the endpoint accepts it. Whether
  this was considered is not recorded.
- **A stopped person is not told** except at sign-in, and only in English and UTC. Specs/049 worded it in
  the reader's language and time; later work added a notification and an email.
- **Nothing closes a shop or removes `Seller`.**
