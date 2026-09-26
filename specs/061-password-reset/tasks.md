---
description: "Task list for Password reset"
---

# Tasks: Password reset

> Completed on 2026-09-27, after the feature merged (#144), from the code at that merge, the pull request and
> [docs/features/auth/security-best-practices.md](../../docs/features/auth/security-best-practices.md).

**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Included and written first on both sides (constitution Principle V): 8 server tests against real
PostgreSQL, 9 storefront tests, two Bruno requests, and an end-to-end run through Mailpit.

## Format: `[ID] [P?] [Story] Description`

The first five tasks are the list as written on 2026-09-25, kept verbatim. T006 onwards break them down.

- [X] T001 [US1] [US2] [US3] Tests first in `server/tests/Ecommerce.Identity.Tests/PasswordResetTests.cs`: the same answer for a known and an unknown address; a hashed token and a queued email with the link; asking again replaces the link; a reset changes the password and ends every session; used, expired and unknown tokens are 400; a concurrent use of one token; no secret in the audit entries or in the sent row
- [X] T002 [US1] [US2] Identity: `Domain/Entities/PasswordResetToken.cs`, configuration + migration, repository, `Application/Auth/Commands/PasswordReset/*`, the `PasswordReset` template, the scrub in `DispatchEmailsCommand`, endpoints in `AuthController`
- [X] T003 [US1] [US2] Storefront tests first, then `pages/forgot-password`, `pages/reset-password`, the sign-in link, `services/auth`, routes, locales
- [X] T004 Bruno requests; end to end through Mailpit
- [X] T005 Mutation checks; docs `docs/features/auth/*`, `docs/features/email.md`, `docs/reference` regenerated, `docs/project/*`, CLAUDE.md

---

## Phase 1: Foundational

- [X] T006 Create `PasswordResetToken` in `server/src/Services/Identity/Ecommerce.Identity.Domain/Entities/PasswordResetToken.cs`
- [X] T007 Create `PasswordResetTokenConfiguration` (table `password_reset_tokens`, char(64) unique `TokenHash`, index and cascading FK on `UserId`) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Configurations/`, and the `PasswordResetTokens` set on `ApplicationDbContext`
- [X] T008 Add migration `20260925035705_AddPasswordResetTokens` under `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Migrations/`
- [X] T009 [P] Add `EmailTemplate.PasswordReset` to `server/src/BuildingBlocks/Ecommerce.Shared/Email/EmailSender.cs`
- [X] T010 Implement `PasswordResetRepository` (`AddAsync`, `DeleteUnusedAsync`, the guarded `TryClaimAsync`) in `server/src/Services/Identity/Ecommerce.Identity.Infrastructure/Persistence/Repositories/PasswordResetRepository.cs` and register it in `DependencyInjection.cs`

## Phase 2: Tests first

- [X] T011 [P] [US1] [US2] [US3] The eight tests in `server/tests/Ecommerce.Identity.Tests/PasswordResetTests.cs` (names in [quickstart.md](quickstart.md)), with the fixture change in `IdentityTestFixture.cs`
- [X] T012 [P] [US1] `client/src/pages/forgot-password/index.test.tsx`: asks for the address typed; the same message whatever the answer; a failed request says so; Vietnamese
- [X] T013 [P] [US2] `client/src/pages/reset-password/index.test.tsx`: sends the token with the password; nothing when the two differ; offers a new link on a refused token; shows the server's password rule; no token means no form

## Phase 3: User Story 1 - ask for a link (P1)

- [X] T014 [US1] `ForgotPasswordCommand`, its validator, `ResetTokens` (new token, hash, 30 minutes) and the forgot handler (delete unused, add, record `PasswordResetRequested`, save, queue the email straight into `outgoing_emails`, one transaction) in `server/src/Services/Identity/Ecommerce.Identity.Application/Auth/Commands/PasswordReset/PasswordReset.cs`
- [X] T015 [US1] The `PasswordReset` template in `vi` and `en` with the `/reset-password?token=` link in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/EmailTemplates.cs`
- [X] T016 [US1] `POST forgot-password` (202) and `RequestLanguage()` in `server/src/Services/Identity/Ecommerce.Identity.WebApi/Controllers/AuthController.cs`
- [X] T017 [P] [US1] `client/src/pages/forgot-password/index.tsx`, the "Forgot password?" link in `client/src/pages/sign-in/index.tsx`, `Auth.forgotPassword` in `client/src/services/auth/index.ts`

## Phase 4: User Story 2 - choose a new password (P1)

- [X] T018 [US2] `ResetPasswordCommand`, its validator (registration's password rules) and the reset handler (claim, set the hash, record `PasswordReset`, save, revoke every refresh token, one transaction) in `PasswordReset.cs`
- [X] T019 [US2] `POST reset-password` (204) in `AuthController.cs`
- [X] T020 [P] [US2] `client/src/pages/reset-password/index.tsx`, both routes in `client/src/routes/index.tsx`, `Auth.resetPassword`, and the words in `client/src/locales/{en,vi}/auth.json`

## Phase 5: User Story 3 - on the record, with nothing secret in it (P2)

- [X] T021 [US3] `EmailTemplates.ScrubbedOnceSent` and the scrub to `{}` on `Sent` in `server/src/Services/Identity/Ecommerce.Identity.Application/Email/DispatchEmailsCommand.cs`

## Phase 6: Polish

- [X] T022 [P] Bruno: `bruno/auth/forgot password is 202 for anybody.yml` and `bruno/security-checks/reset with a made-up token is 400.yml`, passing headless
- [X] T023 End to end against the compose stack with Identity and the storefront rebuilt (output in [quickstart.md](quickstart.md) scenario 4)
- [X] T024 Six mutations (four server, two client), each red, reverted and rebuilt
- [X] T025 [P] Docs: `docs/features/auth/security-best-practices.md` §4.5 and §5, `docs/features/auth/db-design.md`, `docs/features/email.md` rule 7, decision 45 in `docs/project/decisions.md`, timeline, backlog, counts, `CLAUDE.md`; regenerate `docs/reference/{api,data-model}.md`
- [X] T026 Merge through PR #144 (squash, 2026-09-25), closing #103

## Dependencies

T006-T010 before the handlers. T011-T013 before T014-T021. US1 and US2 share `PasswordReset.cs` and are sequential;
the storefront tasks depend only on the contract. T022-T025 after; T026 last.

## Notes

- 26 tasks; the five original ones are the summary, T006-T025 their breakdown.
