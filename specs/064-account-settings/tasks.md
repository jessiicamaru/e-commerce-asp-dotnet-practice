---
description: "Task list for Change your password and your name"
---

# Tasks: Change your password and your name

> Completed on 2026-09-27, after the feature merged (#147), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included and written first on both sides (constitution Principle V), plus Bruno and an end-to-end run
with two sessions.

## Format: `[ID] [P?] [Story] Description`

The first five tasks are the list as written on 2026-09-25, kept verbatim. T006 onwards break them down.

- [X] T001 [US1] [US2] Tests first in `server/tests/Ecommerce.Identity.Tests/AccountTests.cs`. Cover:
  - reading and updating my own profile;
  - validation;
  - `ProfileUpdated` with its diff;
  - a wrong current password is 400, changes nothing and counts toward the pause, and a paused email is 429;
  - the change keeps this session and ends the others;
  - `PasswordChanged` holds no password.
- [X] T002 [US1] [US2] Identity:
  - the commands and handlers;
  - `RevokeOtherRefreshTokensAsync`;
  - the `AuthController` endpoints;
  - the gateway route.
- [X] T003 [US3] Storefront tests first, then:
  - the service, `hooks/account`, the account page's two forms, the words.
- [X] T004 Bruno. Then end to end with two sessions.
- [X] T005 Mutation checks. Docs:
  - security, db and reference;
  - timeline, backlog and counts;
  - CLAUDE.md.

---

## Phase 1: Tests first

- [X] T006 [P] [US1] [US2] The nine tests in `server/tests/Ecommerce.Identity.Tests/AccountTests.cs` (names in [quickstart.md](quickstart.md))
- [X] T007 [P] [US3] `client/src/pages/account/index.test.tsx`: shows and sends my details; a refusal in the server's words; changes my password; nothing sent when the two new passwords differ; a wrong current password beside its field; a 429 as a wait

## Phase 2: User Story 2 - change my name and phone (P1)

- [X] T008 [US2] `AccountProfile`, `GetMeQuery`, `UpdateMeCommand` with its validator, and their handlers (caller from `ICurrentUser`, `ProfileUpdated` before the save) in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/Account/Account.cs`
- [X] T009 [US2] `GET me` and `PUT me`, both `[Authorize]`, in `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/AuthController.cs`

## Phase 3: User Story 1 - change my password (P1)

- [X] T010 [US1] `RevokeOtherRefreshTokensAsync(userId, keep, now)` on `server/src/Services/Identity/Ecommerce.Identity.Application/Common/Interfaces/IUserRepository.cs` and `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/UserRepository.cs`
- [X] T011 [US1] `ChangePasswordCommand` (init-only `KeepRefreshToken`), its validator (registration's rules) and handler (pause first, count a wrong current password, then hash + `PasswordChanged` + save + revoke others in one transaction, then clear the count) in `Account.cs`
- [X] T012 [US1] `PUT me/password` (`[Authorize]`, 204) in `AuthController.cs`, filling `KeepRefreshToken` from the `refreshToken` cookie
- [X] T013 [US1] `auth-change-password-route` under the `sign-in` policy in `server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json`

## Phase 4: User Story 3 - the account page (P1)

- [X] T014 [P] [US3] `Auth.me`, `Auth.updateMe`, `Auth.changePassword` in `client/src/services/auth/index.ts`, their types in `types.ts`, and a query key in `client/src/constants/query-keys/index.ts`
- [X] T015 [P] [US3] `useMe`, `useUpdateMe` and `useChangePassword` in `client/src/hooks/me/index.ts`
- [X] T016 [US3] "Your details" and "Change password" forms in `client/src/pages/account/index.tsx`, renewing the session after a details change; the words in `client/src/locales/{en,vi}/auth.json`

## Phase 5: Polish

- [X] T017 [P] Bruno: `auth/my details.yml`, `auth/change my details.yml`, `auth/changing my password with a wrong current one is 400.yml`, `auth/change my password.yml`, `security-checks/my details without a token is 401.yml` (and `asking for reset links too fast is 429.yml` renumbered to stay last)
- [X] T018 Five mutations, each caught; Identity 136/136, client 329/329; the `auth` folder 20/20 after one fix
- [X] T019 End to end with two cookie jars (output in [quickstart.md](quickstart.md) scenario 2)
- [X] T020 [P] Docs: security §4.9 and §5; decision 48; counts, timeline, backlog, `CLAUDE.md`; `docs/reference/{api,gateway}.md` regenerated
- [X] T021 Merge through PR #147 (squash, 2026-09-25), closing #104

## Dependencies

T006-T007 before the implementation. US2 and US1 share `Account.cs` and the controller, so they are sequential; T010
before T011. US3 depends only on the contract. T017-T020 after; T021 last.

## Notes

- 21 tasks; the five original ones are the summary, T006-T020 their breakdown.
- No migration: the feature writes existing columns only.
