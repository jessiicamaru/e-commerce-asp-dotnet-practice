# Implementation Plan: Change your password and your name

> Completed on 2026-09-27, after the feature merged (#147), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Branch**: `064-account-settings` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md) | **Issue**: #104

## Summary

Three signed-in endpoints in Identity - read my details, change my name and phone, change my password - and two
forms on the storefront's account page. A password change checks the current password, counts a wrong one toward
the sign-in pause of specs/062, and ends every session but the one named by the request's HttpOnly cookie. No
schema change. Decisions below and in [research.md](research.md) (decision 48 in
[docs/project/decisions.md](../../docs/project/decisions.md)).

## Technical Context

**Language/Version**: C# 13 / .NET 10.0; TypeScript, React 19 in the storefront

**Primary Dependencies**: MediatR 12.4.1, FluentValidation 12.1.1, the `ISignInThrottle` of specs/062, EF Core
`ExecuteUpdate`; TanStack Query, react-i18next

**Storage**: No schema change. Writes existing columns of `users` and `refresh_tokens` and rows of
`sign_in_throttles` in `ecommerce_identity_db` (5435)

**Testing**: `AccountTests` (9) against real PostgreSQL; Vitest for the account page (6 new); Bruno (5 requests); an
end-to-end run with two cookie jars

**Target Platform**: Identity (5056) through the gateway (5000); the storefront

**Project Type**: Feature in Identity plus a storefront page

**Performance Goals**: None

**Constraints**: identity from the token, the kept session from the cookie - never from the body; no faster
guessing than sign-in

**Scale/Scope**: Three endpoints, one repository method, one gateway route, one page

## Design

- **Commands** in `Application/Auth/Commands/Account/Account.cs`:
  - `GetMeQuery` → `AccountProfile(Email, FirstName, LastName, Phone, EmailConfirmed)`.
  - `UpdateMeCommand(FirstName, LastName, Phone)`.
  - `ChangePasswordCommand(CurrentPassword, NewPassword)`, with an init-only `KeepRefreshToken` that the
    controller fills from the HttpOnly cookie, never from the body.

  All three read the caller from `ICurrentUser`.
- **The password change**, in order:
  1. If the email's sign-in is paused, answer 429.
  2. Check the current password. When it is wrong, count the failure and answer 400.
  3. Otherwise set the hash, record the audit entry, save.
  4. Revoke the refresh tokens with `IUserRepository.RevokeOtherRefreshTokensAsync(userId, keep, now)`:
     every active one except the cookie's. With no cookie, every one.
- **Routes:** `AuthController` gets `GET me`, `PUT me` and `PUT me/password`, all `[Authorize]`. The
  gateway gives `/api/auth/me/password` the `sign-in` limit.
- **Storefront:**
  - `Auth.me()`, `Auth.updateMe()`, `Auth.changePassword()`;
  - `hooks/account` (`useMe`, `useUpdateMe`, `useChangePassword`); *as built the folder is
    `client/src/hooks/me/`*;
  - the account page with the two forms. A profile change calls `refreshSession()`.
- **Bruno:** in `auth/`, "my profile", "update my name", "changing my password with a wrong current one
  is 400" and "change my password" (on a throwaway account the pre-request registers). In
  `security-checks/`, "my profile without a token is 401". *Correction (2026-09-27):* the requests were committed
  as `auth/my details`, `auth/change my details`, `auth/changing my password with a wrong current one is 400`,
  `auth/change my password` and `security-checks/my details without a token is 401`.

## Decisions

1. **The current session is kept; the others end.** The person who just proved the password should not be
   signed out. Whoever else holds a session, possibly the reason for the change, loses it. It is
   identified by the refresh cookie, which the server set and the script cannot read.
2. **A wrong current password counts against the sign-in pause.** Otherwise this endpoint is a way round
   specs/062 for anybody holding a stolen access token.
3. **No email change.** An unconfirmed new address would undo specs/063.

## Constitution check

- **I, II:** Identity only, layered. Pass.
- **III:** the password, the audit entry and the revocation run in one transaction. Pass.
- **IV:** the caller comes from the token. The kept session comes from the cookie, never the body. Pass.
- **V:** tests against real PostgreSQL, mutation checks, and an end-to-end run with two sessions. Pass.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution.md](../../.specify/memory/constitution.md). The list above is the check as first
written; this table states it in the standard form.

| Principle | Assessment |
| :--- | :--- |
| **I. Service Autonomy** | **Pass.** Identity only: the account, its sessions and its throttle are all Identity's own |
| **II. Clean Architecture Layering** | **Pass.** Queries, commands, validators and handlers in `Application/Auth/Commands/Account/`; `RevokeOtherRefreshTokensAsync` declared on `IUserRepository` and implemented in Infrastructure; the controller only dispatches (and reads the cookie, a transport concern) |
| **III. Atomic Writes and Idempotent Messaging** | **Pass.** The new password hash, the `PasswordChanged` entry and the revocation of the other sessions run in one transaction; the details change and its `ProfileUpdated` entry share one save. The failure count is the single atomic statement of specs/062 |
| **IV. Identity Comes From the Token** | **Pass.** The caller comes from `ICurrentUser`; no command carries a user id. The session to keep comes from the HttpOnly cookie, never the body - the controller overwrites the command's `KeepRefreshToken` |
| **V. Evidence Over Assumption** | **Pass.** Tests against real PostgreSQL, five mutation checks, and an end-to-end run with two sessions (two cookie jars) recorded in PR #147 |

**Post-design re-check**: no violations.

## Project Structure

### Documentation (this feature)

```text
specs/064-account-settings/
├── spec.md
├── plan.md                  # This file
├── research.md              # Five decisions
├── data-model.md            # No schema change; the columns written
├── quickstart.md
├── contracts/
│   └── http-api.md          # GET/PUT /api/auth/me, PUT /api/auth/me/password
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (touched by #147)

```text
server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json                         # auth-change-password-route, sign-in
server/src/Services/Identity/
├── Ecommerce.Identity.Application/Auth/Commands/Account/Account.cs                  # GetMe, UpdateMe, ChangePassword
├── Ecommerce.Identity.Application/Common/Interfaces/IUserRepository.cs              # RevokeOtherRefreshTokensAsync
├── Ecommerce.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs
└── Ecommerce.Identity.WebApi/Controllers/AuthController.cs                          # three endpoints
server/tests/Ecommerce.Identity.Tests/AccountTests.cs
client/src/
├── pages/account/{index.tsx,index.test.tsx}
├── hooks/me/index.ts                                                                 # useMe, useUpdateMe, useChangePassword
├── services/auth/{index.ts,types.ts}
├── constants/query-keys/index.ts
└── locales/{en,vi}/auth.json
bruno/auth/{my details,change my details,changing my password with a wrong current one is 400,change my password}.yml
bruno/security-checks/my details without a token is 401.yml
```

Documentation touched in the same change: `CLAUDE.md`, `docs/features/auth/security-best-practices.md` (§4.9, §5),
`docs/overview/project-overview.md`, `docs/project/{backlog,decisions,timeline}.md`,
`docs/reference/{api,gateway}.md` (regenerated), `docs/testing/testing-strategy.md`.

## Complexity Tracking

> No Constitution Check violations to justify. Table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| :--- | :--- | :--- |
| - | - | - |

## What this feature does not finish

- **Another browser's access token** keeps working until it expires (up to 15 minutes); only its refresh fails.
  [specs/065](../065-revoke-access-tokens/) closed that.
- No email tells the person their password was changed.
- The email address cannot be changed.
